using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.RateLimiting;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>
/// Covers client address resolution behind a proxy (PLAN.md 3.3, backlog A5).
/// </summary>
/// <remarks>
/// Getting this wrong fails in one of two costly ways: too trusting lets a caller mint a new
/// address per request and ignore the anonymous tier, too strict collapses the whole world
/// into one bucket and locks everyone out. Both are covered below.
/// </remarks>
public sealed class ProxyProviderTests
{
    [Fact]
    public void Without_a_provider_forwarded_headers_are_ignored()
    {
        // A directly exposed deployment must not believe a header the caller wrote.
        var context = ContextWith(
            peer: "203.0.113.9",
            headers: new()
            {
                ["x-forwarded-for"] = "1.1.1.1",
                ["x-vercel-forwarded-for"] = "2.2.2.2",
                ["CF-Connecting-IP"] = "3.3.3.3",
            });

        var identity = ResolverFor(ProxyProvider.None).Resolve(context);

        Assert.Equal("ip:203.0.113.9", identity.Key);
    }

    [Fact]
    public void Vercel_prefers_its_own_header()
    {
        // x-vercel-forwarded-for survives another proxy being placed on top of Vercel, so it
        // is consulted before the standard header.
        var context = ContextWith(
            peer: "10.0.0.1",
            headers: new()
            {
                ["x-vercel-forwarded-for"] = "198.51.100.7",
                ["x-forwarded-for"] = "192.0.2.1",
            });

        var identity = ResolverFor(ProxyProvider.Vercel).Resolve(context);

        Assert.Equal("ip:198.51.100.7", identity.Key);
    }

    [Fact]
    public void Vercel_falls_back_to_the_standard_header()
    {
        var context = ContextWith("10.0.0.1", new() { ["x-forwarded-for"] = "192.0.2.1" });

        Assert.Equal("ip:192.0.2.1", ResolverFor(ProxyProvider.Vercel).Resolve(context).Key);
    }

    [Fact]
    public void Cloudflare_reads_its_own_header()
    {
        var context = ContextWith("10.0.0.1", new() { ["CF-Connecting-IP"] = "198.51.100.4" });

        Assert.Equal("ip:198.51.100.4", ResolverFor(ProxyProvider.Cloudflare).Resolve(context).Key);
    }

    [Fact]
    public void The_leftmost_entry_of_a_chain_is_the_client()
    {
        var context = ContextWith("10.0.0.1", new() { ["x-forwarded-for"] = "198.51.100.7, 10.0.0.5, 10.0.0.6" });

        Assert.Equal("ip:198.51.100.7", ResolverFor(ProxyProvider.Vercel).Resolve(context).Key);
    }

    [Fact]
    public void A_missing_or_unparseable_header_falls_back_to_the_peer()
    {
        // Losing the header must not collapse callers into a single "unknown" bucket while a
        // real peer address is available.
        var context = ContextWith("203.0.113.9", new() { ["x-forwarded-for"] = "not-an-address" });

        Assert.Equal("ip:203.0.113.9", ResolverFor(ProxyProvider.Vercel).Resolve(context).Key);
    }

    [Fact]
    public void Ipv6_callers_are_grouped_by_their_sixty_four_bit_prefix()
    {
        // One subscriber is routinely handed a whole /64; partitioning on the full address
        // would give them an unlimited supply of fresh buckets.
        var first = ContextWith("10.0.0.1", new() { ["x-forwarded-for"] = "2001:db8:1234:5678:1::1" });
        var second = ContextWith("10.0.0.1", new() { ["x-forwarded-for"] = "2001:db8:1234:5678:9::abcd" });

        var resolver = ResolverFor(ProxyProvider.Vercel);

        Assert.Equal(resolver.Resolve(first).Key, resolver.Resolve(second).Key);
        Assert.EndsWith("/64", resolver.Resolve(first).Key, StringComparison.Ordinal);
    }

    [Fact]
    public void Different_ipv6_prefixes_stay_separate()
    {
        var first = ContextWith("10.0.0.1", new() { ["x-forwarded-for"] = "2001:db8:1234:5678::1" });
        var second = ContextWith("10.0.0.1", new() { ["x-forwarded-for"] = "2001:db8:1234:9999::1" });

        var resolver = ResolverFor(ProxyProvider.Vercel);

        Assert.NotEqual(resolver.Resolve(first).Key, resolver.Resolve(second).Key);
    }

    [Fact]
    public void An_api_key_takes_precedence_over_any_address()
    {
        var context = ContextWith("10.0.0.1", new()
        {
            ["x-forwarded-for"] = "198.51.100.7",
            ["X-API-Key"] = "abc123",
        });

        var identity = ResolverFor(ProxyProvider.Vercel).Resolve(context);

        Assert.Equal(ClientTier.ApiKey, identity.Tier);
        Assert.Equal("key:abc123", identity.Key);
    }

    [Fact]
    public void Every_configured_provider_needs_the_forwarded_headers_middleware()
    {
        // Not only Generic: TLS terminates at the vendor's edge for Vercel and Cloudflare too,
        // so Request.Scheme needs the same correction or the OpenAPI document's generated
        // "servers" URL comes out as http:// and Scalar's "Try it" trips mixed-content blocking
        // on an https docs page.
        Assert.False(new ProxyOptions { Provider = ProxyProvider.None }.UseForwardedHeadersMiddleware);
        Assert.True(new ProxyOptions { Provider = ProxyProvider.Vercel }.UseForwardedHeadersMiddleware);
        Assert.True(new ProxyOptions { Provider = ProxyProvider.Cloudflare }.UseForwardedHeadersMiddleware);
        Assert.True(new ProxyOptions { Provider = ProxyProvider.Generic }.UseForwardedHeadersMiddleware);
    }

    [Fact]
    public void Vendor_providers_only_trust_the_proto_header_not_forwarded_for()
    {
        // The client address middleware doesn't recognise x-vercel-forwarded-for or
        // CF-Connecting-IP, so letting it also rewrite RemoteIpAddress from a plain
        // X-Forwarded-For would be a second, redundant source of truth for a value
        // ClientIdentityResolver already ignores once a vendor header is present.
        Assert.Equal(ForwardedHeaders.XForwardedProto, new ProxyOptions { Provider = ProxyProvider.Vercel }.ForwardedHeadersToTrust);
        Assert.Equal(ForwardedHeaders.XForwardedProto, new ProxyOptions { Provider = ProxyProvider.Cloudflare }.ForwardedHeadersToTrust);
        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            new ProxyOptions { Provider = ProxyProvider.Generic }.ForwardedHeadersToTrust);
    }

    // A true end-to-end check (fetch /openapi/v1.json with X-Forwarded-Proto: https and assert
    // the generated "servers" entry is https://) was tried and dropped: WebApplicationFactory's
    // in-memory TestServer transport leaves Connection.RemoteIpAddress null, which
    // ForwardedHeadersMiddleware treats as untrusted no matter how KnownProxies/KnownNetworks
    // are configured, and IStartupFilter ordering could not reliably inject a fake peer address
    // ahead of it on this minimal-hosting entry point. The underlying fix was instead verified
    // directly against a real Kestrel server: with ForwardedHeadersOptions.KnownProxies/
    // KnownNetworks cleared, a request from a real peer with X-Forwarded-Proto: https flips
    // Request.Scheme (and IsHttps) to https, and without the header it stays http — confirming
    // ForwardedHeadersToTrust below is what the OpenAPI "servers" URL and Scalar's "Try it"
    // actually need.

    private static ClientIdentityResolver ResolverFor(ProxyProvider provider)
    {
        var options = new OpenDataOptions();
        options.Proxy.Provider = provider;

        return new ClientIdentityResolver(Options.Create(options));
    }

    private static DefaultHttpContext ContextWith(string peer, Dictionary<string, string> headers)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);

        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        return context;
    }
}
