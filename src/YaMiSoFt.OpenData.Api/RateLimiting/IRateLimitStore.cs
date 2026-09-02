using YaMiSoFt.OpenData.Api.Configuration;

namespace YaMiSoFt.OpenData.Api.RateLimiting;

/// <summary>Outcome of a rate limit check, carrying everything the response headers need.</summary>
/// <param name="Allowed">False when the request must be rejected with 429.</param>
/// <param name="Limit">Allowance of the binding window.</param>
/// <param name="Remaining">Requests left in the binding window.</param>
/// <param name="ResetsAt">When the binding window rolls over.</param>
public readonly record struct RateLimitDecision(
    bool Allowed,
    int Limit,
    int Remaining,
    DateTimeOffset ResetsAt)
{
    /// <summary>Seconds until the window resets, never negative. Feeds <c>Retry-After</c>.</summary>
    public int RetryAfterSeconds(DateTimeOffset now) =>
        (int)Math.Max(1, Math.Ceiling((ResetsAt - now).TotalSeconds));
}

/// <summary>
/// Counts requests per caller. The abstraction is the seam between the single-node MVP and
/// the multi-replica deployment: NFR-01 asks for the in-process limiter while NFR-04 asks for
/// consistent counters across pods, and only one implementation can be active at a time
/// (PLAN.md 3.1). Faz 2 adds a Redis-backed implementation behind this same interface.
/// </summary>
public interface IRateLimitStore
{
    /// <summary>
    /// True when counters are shared across instances. A false value on an autoscaling
    /// platform means every limit is effectively multiplied by the instance count, so the
    /// application logs a warning at startup rather than letting it pass unnoticed.
    /// </summary>
    bool IsDistributed { get; }

    /// <summary>
    /// Records one request against <paramref name="partitionKey"/> and reports whether it
    /// stays within <paramref name="tier"/>.
    /// </summary>
    ValueTask<RateLimitDecision> AcquireAsync(
        string partitionKey,
        TierOptions tier,
        CancellationToken cancellationToken = default);
}
