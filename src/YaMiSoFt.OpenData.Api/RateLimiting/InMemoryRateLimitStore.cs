using Microsoft.Extensions.Caching.Memory;
using YaMiSoFt.OpenData.Api.Configuration;

namespace YaMiSoFt.OpenData.Api.RateLimiting;

/// <summary>
/// Single-node rate limit counters held in process memory.
/// </summary>
/// <remarks>
/// Correct only while one instance serves all traffic: counters are per process, so N
/// instances multiply every limit by N. That is fine for local development and a single
/// container, and wrong on any autoscaling platform — see <see cref="RedisRateLimitStore"/>.
///
/// Expiry does the housekeeping: each counter entry is evicted when its window closes, so
/// idle partitions cost nothing and no sweep task is needed.
/// </remarks>
public sealed class InMemoryRateLimitStore(IMemoryCache cache, TimeProvider timeProvider)
    : IRateLimitStore
{
    /// <inheritdoc />
    public bool IsDistributed => false;

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> AcquireAsync(
        string partitionKey,
        TierOptions tier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentNullException.ThrowIfNull(tier);
        cancellationToken.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow();

        // The minute window is charged first. If it rejects, the daily counter is left alone:
        // a caller who merely burst should not also lose their daily allowance for it.
        var minute = Charge(partitionKey, RateLimitWindows.Minute, tier.PerMinute, now);
        if (!minute.Allowed)
        {
            return ValueTask.FromResult(minute);
        }

        var day = Charge(partitionKey, RateLimitWindows.Day, tier.PerDay, now);
        if (!day.Allowed)
        {
            return ValueTask.FromResult(day);
        }

        // Both windows allow the request; report whichever is closer to exhaustion so the
        // caller can pace against the constraint that will actually bite them.
        return ValueTask.FromResult(minute.Remaining <= day.Remaining ? minute : day);
    }

    private RateLimitDecision Charge(
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

        var counter = cache.GetOrCreate(window.KeyFor(partitionKey, spec.Name), entry =>
        {
            entry.AbsoluteExpiration = window.ResetsAt;
            entry.Size = 1;
            return new Counter();
        })!;

        return window.Evaluate(counter.Increment(), limit);
    }

    private sealed class Counter
    {
        private int _value;

        internal int Increment() => Interlocked.Increment(ref _value);
    }
}
