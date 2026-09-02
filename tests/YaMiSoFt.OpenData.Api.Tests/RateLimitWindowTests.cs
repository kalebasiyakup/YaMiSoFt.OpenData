using YaMiSoFt.OpenData.Api.RateLimiting;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>
/// Covers the counting window shared by the in-memory and Redis stores.
/// </summary>
/// <remarks>
/// This type is the reason the two stores can be swapped without limits behaving differently,
/// so its alignment and key derivation are pinned here rather than only being exercised
/// indirectly through whichever store happens to be configured.
/// </remarks>
public sealed class RateLimitWindowTests
{
    [Fact]
    public void Minute_windows_align_to_the_clock_not_to_first_use()
    {
        // 12:34:56 belongs to the window that started at 12:34:00 and ends at 12:35:00 —
        // not to one starting at 12:34:56. That is what makes X-RateLimit-Reset exact.
        var now = new DateTimeOffset(2026, 9, 1, 12, 34, 56, TimeSpan.Zero);

        var window = RateLimitWindow.For(now, TimeSpan.FromMinutes(1));

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 12, 34, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 12, 35, 0, TimeSpan.Zero), window.ResetsAt);
    }

    [Fact]
    public void Day_windows_align_to_utc_midnight()
    {
        var now = new DateTimeOffset(2026, 9, 1, 23, 59, 59, TimeSpan.Zero);

        var window = RateLimitWindow.For(now, TimeSpan.FromDays(1));

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), window.Start);
        Assert.Equal(new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero), window.ResetsAt);
    }

    [Fact]
    public void Instants_in_the_same_window_produce_the_same_key()
    {
        var early = RateLimitWindow.For(new DateTimeOffset(2026, 9, 1, 12, 34, 1, TimeSpan.Zero), TimeSpan.FromMinutes(1));
        var late = RateLimitWindow.For(new DateTimeOffset(2026, 9, 1, 12, 34, 59, TimeSpan.Zero), TimeSpan.FromMinutes(1));

        // Two instances handling requests a second apart must address the same counter.
        Assert.Equal(early.KeyFor("ip:1.2.3.4", "m"), late.KeyFor("ip:1.2.3.4", "m"));
    }

    [Fact]
    public void The_next_window_gets_a_different_key()
    {
        var current = RateLimitWindow.For(new DateTimeOffset(2026, 9, 1, 12, 34, 59, TimeSpan.Zero), TimeSpan.FromMinutes(1));
        var next = RateLimitWindow.For(new DateTimeOffset(2026, 9, 1, 12, 35, 0, TimeSpan.Zero), TimeSpan.FromMinutes(1));

        // A fresh key is how a window resets; nothing has to zero a counter.
        Assert.NotEqual(current.KeyFor("ip:1.2.3.4", "m"), next.KeyFor("ip:1.2.3.4", "m"));
    }

    [Fact]
    public void Different_callers_never_share_a_counter()
    {
        var window = RateLimitWindow.For(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        Assert.NotEqual(window.KeyFor("ip:1.2.3.4", "m"), window.KeyFor("ip:1.2.3.5", "m"));
        Assert.NotEqual(window.KeyFor("ip:1.2.3.4", "m"), window.KeyFor("ip:1.2.3.4", "d"));
    }

    [Theory]
    [InlineData(1, 30, true, 29)]
    [InlineData(30, 30, true, 0)]
    [InlineData(31, 30, false, 0)]
    [InlineData(500, 30, false, 0)]
    public void Evaluate_turns_a_usage_count_into_a_decision(long used, int limit, bool allowed, int remaining)
    {
        var window = RateLimitWindow.For(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        var decision = window.Evaluate(used, limit);

        Assert.Equal(allowed, decision.Allowed);
        Assert.Equal(limit, decision.Limit);
        Assert.Equal(remaining, decision.Remaining);
        Assert.Equal(window.ResetsAt, decision.ResetsAt);
    }

    [Fact]
    public void Retry_after_is_never_zero_or_negative()
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 34, 59, 900, TimeSpan.Zero);
        var window = RateLimitWindow.For(now, TimeSpan.FromMinutes(1));

        // A caller told to retry in zero seconds would hammer the endpoint immediately.
        Assert.True(window.Evaluate(99, 30).RetryAfterSeconds(now) >= 1);
    }

    [Fact]
    public void A_non_positive_duration_is_rejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RateLimitWindow.For(DateTimeOffset.UtcNow, TimeSpan.Zero));
}
