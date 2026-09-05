using Microsoft.AspNetCore.HttpOverrides;

namespace YaMiSoFt.OpenData.Api.Configuration;

/// <summary>The platform sitting in front of the application, if any.</summary>
public enum ProxyProvider
{
    /// <summary>
    /// Directly exposed. Forwarded headers are ignored entirely — trusting them here would
    /// let any caller mint a fresh client address per request and bypass rate limiting.
    /// </summary>
    None = 0,

    /// <summary>Vercel. Reads <c>x-vercel-forwarded-for</c>, then <c>x-forwarded-for</c>.</summary>
    Vercel = 1,

    /// <summary>Cloudflare. Reads <c>CF-Connecting-IP</c>.</summary>
    Cloudflare = 2,

    /// <summary>
    /// Another reverse proxy. Uses the standard <c>X-Forwarded-For</c> chain and requires
    /// <see cref="ProxyOptions.KnownProxies"/> or <see cref="ProxyOptions.KnownNetworks"/> to
    /// be populated, since nothing else establishes which hop to trust.
    /// </summary>
    Generic = 3,
}

/// <summary>Reverse proxy trust settings.</summary>
/// <remarks>
/// Behind a CDN every request arrives from the CDN's address. Without this configuration the
/// anonymous tier would partition all of the world's traffic into a single bucket and lock out
/// every caller within seconds of going live (PLAN.md 3.3).
///
/// The provider is named rather than inferred because the safe header differs per platform and
/// guessing wrong fails in one of two bad ways: too trusting lets callers spoof their address,
/// too strict collapses everyone into one bucket.
/// </remarks>
public sealed class ProxyOptions
{
    /// <summary>Which platform's forwarded headers to trust. Defaults to none.</summary>
    public ProxyProvider Provider { get; set; } = ProxyProvider.None;

    /// <summary>Proxy addresses whose forwarded headers are trusted. Used by <see cref="ProxyProvider.Generic"/>.</summary>
    public IList<string> KnownProxies { get; } = [];

    /// <summary>Proxy networks, in CIDR notation, whose forwarded headers are trusted.</summary>
    public IList<string> KnownNetworks { get; } = [];

    /// <summary>
    /// Overrides the header the chosen provider would use. Leave unset unless the platform
    /// documents a different one.
    /// </summary>
    public string? ClientAddressHeader { get; set; }

    /// <summary>True when any forwarded header should be honoured.</summary>
    public bool IsBehindProxy => Provider != ProxyProvider.None;

    /// <summary>
    /// The headers to consult, in order, for the client address.
    /// </summary>
    /// <remarks>
    /// Vercel documents that it overwrites <c>X-Forwarded-For</c> and refuses to forward
    /// external values, specifically to prevent spoofing — so the header can be trusted
    /// without enumerating proxy addresses, which would be impossible for a global edge
    /// network anyway. <c>x-vercel-forwarded-for</c> is preferred because it survives another
    /// proxy being placed on top of Vercel.
    /// </remarks>
    public IReadOnlyList<string> ClientAddressHeaders()
    {
        if (!string.IsNullOrWhiteSpace(ClientAddressHeader))
        {
            return [ClientAddressHeader];
        }

        return Provider switch
        {
            ProxyProvider.Vercel => ["x-vercel-forwarded-for", "x-forwarded-for"],
            ProxyProvider.Cloudflare => ["CF-Connecting-IP"],
            _ => [],
        };
    }

    /// <summary>
    /// True when the standard ForwardedHeaders middleware should run.
    /// </summary>
    /// <remarks>
    /// Every provider needs this now, not only <see cref="ProxyProvider.Generic"/>. TLS
    /// terminates at the vendor's edge and the container only ever sees plain HTTP from it, so
    /// without this <c>Request.Scheme</c> stays "http" no matter what the caller actually used.
    /// That silently breaks anything that reads it: the OpenAPI document's auto-generated
    /// <c>servers</c> entry comes out as <c>http://</c>, and Scalar's "Try it" then issues a
    /// plain-HTTP fetch from an HTTPS docs page, which every browser blocks as mixed content.
    /// </remarks>
    public bool UseForwardedHeadersMiddleware => Provider != ProxyProvider.None;

    /// <summary>
    /// The forwarded headers the middleware should trust.
    /// </summary>
    /// <remarks>
    /// A vendor provider (Vercel, Cloudflare) only needs <see cref="ForwardedHeaders.XForwardedProto"/>
    /// corrected here — its client address comes from <see cref="ClientAddressHeaders"/> instead,
    /// read directly by the rate limiter's identity resolver, because the middleware does not
    /// recognise that header. Letting it also rewrite <c>RemoteIpAddress</c> from a plain
    /// <c>X-Forwarded-For</c> would just be a second, redundant source of truth for a value the
    /// resolver already ignores when a vendor header is present. <see cref="ProxyProvider.Generic"/>
    /// has no vendor header to fall back on, so it needs both.
    /// </remarks>
    public ForwardedHeaders ForwardedHeadersToTrust => Provider == ProxyProvider.Generic
        ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        : ForwardedHeaders.XForwardedProto;
}
