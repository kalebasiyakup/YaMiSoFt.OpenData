using System.Net;
using Microsoft.AspNetCore.Http;
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
    public void Only_a_generic_proxy_needs_the_forwarded_headers_middleware()
    {
        Assert.False(new ProxyOptions { Provider = ProxyProvider.None }.UseForwardedHeadersMiddleware);
        Assert.False(new ProxyOptions { Provider = ProxyProvider.Vercel }.UseForwardedHeadersMiddleware);
        Assert.False(new ProxyOptions { Provider = ProxyProvider.Cloudflare }.UseForwardedHeadersMiddleware);
        Assert.True(new ProxyOptions { Provider = ProxyProvider.Generic }.UseForwardedHeadersMiddleware);
    }

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
