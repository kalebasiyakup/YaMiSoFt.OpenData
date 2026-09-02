namespace YaMiSoFt.OpenData.Api.Configuration;

/// <summary>Root configuration section for the API.</summary>
public sealed class OpenDataOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenData";

    /// <summary>
    /// Dataset directory. Relative paths resolve against the application base directory;
    /// null falls back to the "data" folder shipped with the application.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>Rate limiting configuration (NFR-01..NFR-05).</summary>
    public RateLimitOptions RateLimit { get; set; } = new();

    /// <summary>Caching configuration (NFR-06, NFR-07).</summary>
    public CacheOptions Cache { get; set; } = new();

    /// <summary>Reverse proxy trust configuration (PLAN.md 3.3).</summary>
    public ProxyOptions Proxy { get; set; } = new();
}

/// <summary>Per-tier request allowances (BRD 5.3).</summary>
public sealed class RateLimitOptions
{
    /// <summary>Turns limiting off entirely. Intended for local development and tests.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Header carrying the free-tier API key.</summary>
    public string ApiKeyHeader { get; set; } = "X-API-Key";

    /// <summary>
    /// True when the hosting platform enforces a burst limit at the edge, before a request
    /// reaches the application.
    /// </summary>
    /// <remarks>
    /// This is a statement about the deployment, not a switch that changes behaviour. It tells
    /// the application that in-process counters are a deliberate choice rather than an
    /// oversight, so startup reports the arrangement instead of warning about it.
    ///
    /// Edge limiting is the layer that actually protects against abuse: it rejects traffic
    /// before any compute is billed, and it counts accurately because the edge sees every
    /// request. The in-process counters then cover what the edge cannot express — the daily
    /// window, the per-tier allowances, and the X-RateLimit headers.
    /// </remarks>
    public bool EdgeBurstProtection { get; set; }

    /// <summary>
    /// Connection string for shared counters. Empty means in-process counting, which is exact
    /// on a single instance and approximate once the platform scales out.
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>Anonymous, IP-partitioned tier.</summary>
    public TierOptions Anonymous { get; set; } = new() { PerMinute = 30, PerDay = 1_000 };

    /// <summary>Free API key tier.</summary>
    public TierOptions ApiKey { get; set; } = new() { PerMinute = 120, PerDay = 10_000 };

    /// <summary>
    /// Bulk download tier for <c>/all</c> endpoints (PLAN.md 3.8). One bulk call costs the
    /// same as one ordinary call against the normal counters but transfers orders of
    /// magnitude more bytes, so it gets its own, much tighter allowance.
    /// </summary>
    public TierOptions BulkDownload { get; set; } = new() { PerMinute = 5, PerDay = 100 };
}

/// <summary>Request allowance for a single tier.</summary>
public sealed class TierOptions
{
    /// <summary>Requests permitted per rolling minute window.</summary>
    public int PerMinute { get; set; }

    /// <summary>Requests permitted per rolling day window.</summary>
    public int PerDay { get; set; }
}

/// <summary>Cache lifetimes (NFR-06, NFR-07).</summary>
public sealed class CacheOptions
{
    /// <summary>Lifetime for ordinary reference-data responses.</summary>
    public TimeSpan Default { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Lifetime for responses driven by a search term. Kept short so the cache is not filled
    /// with one entry per distinct term a crawler happens to try (PLAN.md 3.5).
    /// </summary>
    public TimeSpan Search { get; set; } = TimeSpan.FromMinutes(5);
}
