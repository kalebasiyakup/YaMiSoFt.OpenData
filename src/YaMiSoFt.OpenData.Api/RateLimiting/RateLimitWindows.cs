namespace YaMiSoFt.OpenData.Api.RateLimiting;

/// <summary>A named counting window: how long it lasts and what its keys are prefixed with.</summary>
/// <param name="Name">Short key prefix, e.g. "m" or "d".</param>
/// <param name="Duration">Window length.</param>
public readonly record struct RateLimitWindowSpec(string Name, TimeSpan Duration);

/// <summary>
/// The two windows every tier is measured against (BRD 5.3: a per-minute burst limit and a
/// per-day volume limit).
/// </summary>
/// <remarks>
/// Shared by both stores so the window names that end up in keys cannot drift between them.
/// </remarks>
public static class RateLimitWindows
{
    /// <summary>The per-minute burst window.</summary>
    public static readonly RateLimitWindowSpec Minute = new("m", TimeSpan.FromMinutes(1));

    /// <summary>The per-day volume window.</summary>
    public static readonly RateLimitWindowSpec Day = new("d", TimeSpan.FromDays(1));
}
