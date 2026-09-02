using System.Net;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;

namespace YaMiSoFt.OpenData.Api.RateLimiting;

/// <summary>Which allowance a caller is being measured against.</summary>
public enum ClientTier
{
    /// <summary>Unauthenticated caller, partitioned by address (BRD 5.3).</summary>
    Anonymous = 0,

    /// <summary>Caller presenting a free-tier API key.</summary>
    ApiKey = 1,
}

/// <summary>The caller a rate limit partition is keyed on.</summary>
/// <param name="Tier">Allowance that applies.</param>
/// <param name="Key">Stable partition key: the API key, or the client address.</param>
public readonly record struct ClientIdentity(ClientTier Tier, string Key);

/// <summary>
/// Resolves the caller behind a request.
/// </summary>
/// <remarks>
/// Address resolution is deliberately conservative. Forwarded headers are honoured only when a
/// proxy provider is configured, because a caller who can name their own address can also mint
/// a fresh one per request and bypass the anonymous tier entirely.
/// </remarks>
public sealed class ClientIdentityResolver(IOptions<OpenDataOptions> options)
{
    private readonly OpenDataOptions _options = options.Value;

    /// <summary>Resolves the identity for <paramref name="context"/>.</summary>
    public ClientIdentity Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var apiKeyHeader = _options.RateLimit.ApiKeyHeader;

        if (!string.IsNullOrWhiteSpace(apiKeyHeader) &&
            context.Request.Headers.TryGetValue(apiKeyHeader, out var apiKey) &&
            apiKey.ToString() is { Length: > 0 } key)
        {
            // Faz 2 replaces this with a lookup against the key store; until the portal
            // exists, presenting any key only selects the tier, it does not authenticate.
            return new ClientIdentity(ClientTier.ApiKey, $"key:{key}");
        }

        return new ClientIdentity(ClientTier.Anonymous, $"ip:{ResolveAddress(context)}");
    }

    private string ResolveAddress(HttpContext context)
    {
        var proxy = _options.Proxy;

        if (proxy.IsBehindProxy)
        {
            foreach (var header in proxy.ClientAddressHeaders())
            {
                if (context.Request.Headers.TryGetValue(header, out var values) &&
                    TryParseFirst(values.ToString(), out var forwarded))
                {
                    return Normalize(forwarded);
                }
            }
        }

        // For a generic proxy the ForwardedHeaders middleware has already rewritten this from
        // X-Forwarded-For; otherwise it is the real peer address.
        var remote = context.Connection.RemoteIpAddress;
        return remote is null ? "unknown" : Normalize(remote);
    }

    /// <summary>
    /// Reads the client address from a forwarded header. The value may be a comma-separated
    /// chain, in which case the leftmost entry is the original client.
    /// </summary>
    private static bool TryParseFirst(string headerValue, out IPAddress address)
    {
        foreach (var candidate in headerValue.Split(',', StringSplitOptions.TrimEntries))
        {
            // Some proxies append a port; IPAddress.TryParse rejects that, so try both forms.
            if (IPAddress.TryParse(candidate, out var parsed))
            {
                address = parsed;
                return true;
            }

            var colon = candidate.LastIndexOf(':');
            if (colon > 0 && IPAddress.TryParse(candidate.AsSpan(0, colon), out parsed))
            {
                address = parsed;
                return true;
            }
        }

        address = IPAddress.None;
        return false;
    }

    /// <summary>
    /// Collapses an address to its partition key. IPv6 callers are grouped by /64 because a
    /// single residential subscriber is routinely handed that whole range, and partitioning
    /// on the full address would hand them an unlimited supply of fresh buckets.
    /// </summary>
    private static string Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes).ToString() + "/64";
    }
}
