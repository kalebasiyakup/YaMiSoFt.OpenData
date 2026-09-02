using StackExchange.Redis;
using YaMiSoFt.OpenData.Api.Configuration;

namespace YaMiSoFt.OpenData.Api.RateLimiting;

/// <summary>
/// Rate limit counters shared across every instance, held in Redis (NFR-04).
/// </summary>
/// <remarks>
/// This is what makes the published limits mean anything on an autoscaling platform. With
/// per-process counters, a caller's 30 requests per minute becomes 30 per minute
/// <em>per instance</em>, so the effective limit rises with load — exactly backwards.
///
/// The counting is a single Lua script so increment and expiry are one atomic step. Doing
/// them as two round trips leaves a window where a crash between them strands a key with no
/// expiry, which would lock a caller out permanently rather than until the window closes.
/// The window key already encodes its own start instant (see <see cref="RateLimitWindow"/>),
/// so nothing ever has to reset a counter; the key simply stops being addressed.
/// </remarks>
public sealed class RedisRateLimitStore(
    IConnectionMultiplexer connection,
    TimeProvider timeProvider,
    ILogger<RedisRateLimitStore> logger) : IRateLimitStore
{
    /// <summary>
    /// Increments the counter and, on the first hit of a window, sets its expiry.
    /// Returns the post-increment count.
    /// </summary>
    private const string CountScript = """
        local used = redis.call('INCR', KEYS[1])
        if used == 1 then
          redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return used
        """;

    /// <inheritdoc />
    public bool IsDistributed => true;

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> AcquireAsync(
        string partitionKey,
        TierOptions tier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentNullException.ThrowIfNull(tier);
        cancellationToken.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow();

        try
        {
            // Charged in the same order as the in-memory store: a caller who merely burst
            // should not also lose their daily allowance for it.
            var minute = await ChargeAsync(partitionKey, RateLimitWindows.Minute, tier.PerMinute, now)
                .ConfigureAwait(false);

            if (!minute.Allowed)
            {
                return minute;
            }

            var day = await ChargeAsync(partitionKey, RateLimitWindows.Day, tier.PerDay, now)
                .ConfigureAwait(false);

            if (!day.Allowed)
            {
                return day;
            }

            return minute.Remaining <= day.Remaining ? minute : day;
        }
        catch (RedisException ex)
        {
            // Fail open. This is a free, read-only, public-data API: a Redis outage that
            // turned every request into a 500 would be a far worse failure than briefly
            // unmetered traffic, and the CDN still absorbs most of the load. The counters
            // resume as soon as Redis is back.
            logger.LogError(ex, "Rate limit backend unavailable; allowing the request unmetered.");

            var window = RateLimitWindow.For(now, RateLimitWindows.Minute.Duration);
            return new RateLimitDecision(true, tier.PerMinute, tier.PerMinute, window.ResetsAt);
        }
    }

    private async Task<RateLimitDecision> ChargeAsync(
        string partitionKey,
        RateLimitWindowSpec spec,
        int limit,
        DateTimeOffset now)
    {
        var window = RateLimitWindow.For(now, spec.Duration);

        if (limit <= 0)
        {
            // A non-positive limit means "unmetered" rather than "always reject".
            return new RateLimitDecision(true, int.MaxValue, int.MaxValue, window.ResetsAt);
        }

        var key = (RedisKey)window.KeyFor(partitionKey, spec.Name);

        // The expiry is the remaining life of the window, not its full length: a key created
        // near the end of a window must still die with that window.
        var remaining = window.ResetsAt - now;
        var ttlMilliseconds = (long)Math.Max(1_000, remaining.TotalMilliseconds);

        var used = (long)await connection
            .GetDatabase()
            .ScriptEvaluateAsync(CountScript, [key], [ttlMilliseconds])
            .ConfigureAwait(false);

        return window.Evaluate(used, limit);
    }
}
