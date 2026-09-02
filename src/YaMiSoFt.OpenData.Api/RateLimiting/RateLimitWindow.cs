using System.Globalization;

namespace YaMiSoFt.OpenData.Api.RateLimiting;

/// <summary>
/// A fixed counting window aligned to the wall clock.
/// </summary>
/// <remarks>
/// Alignment is what makes <c>X-RateLimit-Reset</c> an exact timestamp instead of an estimate:
/// a minute window always ends on a minute boundary and a day window at UTC midnight,
/// regardless of when the first request arrived.
///
/// It is also what lets the in-memory and Redis stores behave identically. Both derive the
/// same key for the same caller and instant, so a deployment that scales from one instance to
/// many — or swaps stores entirely — changes where the counters live, not how limits behave.
/// The Redis implementation is then just <c>INCR</c> on that key with an expiry set to the
/// window's end.
/// </remarks>
/// <param name="Start">Inclusive start of the window.</param>
/// <param name="ResetsAt">Exclusive end of the window.</param>
public readonly record struct RateLimitWindow(DateTimeOffset Start, DateTimeOffset ResetsAt)
{
    /// <summary>How long the window lasts.</summary>
    public TimeSpan Duration => ResetsAt - Start;

    /// <summary>
    /// Returns the window of length <paramref name="duration"/> containing <paramref name="now"/>.
    /// </summary>
    public static RateLimitWindow For(DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Window duration must be positive.");
        }

        var start = new DateTimeOffset(now.UtcTicks - (now.UtcTicks % duration.Ticks), TimeSpan.Zero);

        return new RateLimitWindow(start, start + duration);
    }

    /// <summary>
    /// Builds the counter key for a caller in this window. The window start is part of the key,
    /// so a new window starts from zero without anything having to reset the old counter.
    /// </summary>
    public string KeyFor(string partitionKey, string windowName) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"rl:{windowName}:{Start.UtcTicks}:{partitionKey}");

    /// <summary>Turns a raw usage count into the decision the middleware reports.</summary>
    public RateLimitDecision Evaluate(long used, int limit) => new(
        Allowed: used <= limit,
        Limit: limit,
        Remaining: (int)Math.Max(0, limit - used),
        ResetsAt: ResetsAt);
}
