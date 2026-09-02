using System.Net;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>Covers the caching and rate limiting contracts (NFR-01..NFR-07).</summary>
public sealed class CachingAndRateLimitTests
{
    [Fact]
    public async Task Responses_carry_cache_headers_and_a_weak_etag()
    {
        using var factory = new OpenDataApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/provinces/34", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromDays(1), response.Headers.CacheControl?.MaxAge);
        Assert.NotNull(response.Headers.ETag);
        Assert.True(response.Headers.ETag!.IsWeak);
    }

    [Fact]
    public async Task Conditional_request_is_answered_with_304_and_no_body()
    {
        using var factory = new OpenDataApiFactory();
        using var client = factory.CreateClient();

        using var first = await client.GetAsync(new Uri("/api/v1/provinces/34", UriKind.Relative));
        var etag = first.Headers.ETag!;

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/provinces/34", UriKind.Relative));
        request.Headers.IfNoneMatch.Add(etag);

        using var second = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Empty(await second.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Equivalent_requests_written_differently_share_one_etag()
    {
        using var factory = new OpenDataApiFactory();
        using var client = factory.CreateClient();

        // Field order is normalized into the cache key, so these are one response, not two
        // (PLAN.md 3.5).
        using var first = await client.GetAsync(
            new Uri("/api/v1/provinces?fields=id,name&pageSize=5", UriKind.Relative));
        using var second = await client.GetAsync(
            new Uri("/api/v1/provinces?fields=name,id&pageSize=5", UriKind.Relative));

        Assert.Equal(first.Headers.ETag, second.Headers.ETag);
    }

    [Fact]
    public async Task Every_response_reports_remaining_quota()
    {
        using var factory = new OpenDataApiFactory().With("OpenData:RateLimit:Enabled", "true");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/provinces/34", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));
        Assert.True(response.Headers.Contains("X-RateLimit-Reset"));
    }

    [Fact]
    public async Task Exhausted_allowance_returns_429_with_retry_after()
    {
        using var factory = new OpenDataApiFactory()
            .With("OpenData:RateLimit:Enabled", "true")
            .With("OpenData:RateLimit:Anonymous:PerMinute", "3")
            .With("OpenData:RateLimit:Anonymous:PerDay", "100");
        using var client = factory.CreateClient();

        HttpResponseMessage? rejected = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.GetAsync(new Uri("/api/v1/provinces/34", UriKind.Relative));

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rejected = response;
                break;
            }

            response.Dispose();
        }

        Assert.NotNull(rejected);
        Assert.Equal("application/problem+json", rejected!.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.Equal("0", rejected.Headers.GetValues("X-RateLimit-Remaining").Single());

        rejected.Dispose();
    }

    [Fact]
    public async Task Bulk_downloads_are_metered_separately_from_paged_reads()
    {
        using var factory = new OpenDataApiFactory()
            .With("OpenData:RateLimit:Enabled", "true")
            .With("OpenData:RateLimit:BulkDownload:PerMinute", "1")
            .With("OpenData:RateLimit:BulkDownload:PerDay", "10")
            .With("OpenData:RateLimit:Anonymous:PerMinute", "60")
            .With("OpenData:RateLimit:Anonymous:PerDay", "1000");
        using var client = factory.CreateClient();

        using var firstBulk = await client.GetAsync(new Uri("/api/v1/provinces/all", UriKind.Relative));
        using var secondBulk = await client.GetAsync(new Uri("/api/v1/provinces/all", UriKind.Relative));

        firstBulk.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.TooManyRequests, secondBulk.StatusCode);

        // Spending the bulk allowance must not lock the caller out of ordinary reads.
        using var paged = await client.GetAsync(new Uri("/api/v1/provinces?pageSize=1", UriKind.Relative));
        paged.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Health_endpoints_are_never_rate_limited()
    {
        using var factory = new OpenDataApiFactory()
            .With("OpenData:RateLimit:Enabled", "true")
            .With("OpenData:RateLimit:Anonymous:PerMinute", "1")
            .With("OpenData:RateLimit:Anonymous:PerDay", "1");
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
            using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

            live.EnsureSuccessStatusCode();
            ready.EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Cors_allows_any_origin_for_reads_only()
    {
        using var factory = new OpenDataApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/v1/provinces", UriKind.Relative));
        request.Headers.Add("Origin", "https://example.test");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(request);

        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var allowed = response.Headers.GetValues("Access-Control-Allow-Methods").Single();
        Assert.Contains("GET", allowed, StringComparison.Ordinal);
        Assert.DoesNotContain("POST", allowed, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE", allowed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        using var factory = new OpenDataApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/provinces/34", UriKind.Relative));

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        using var factory = new OpenDataApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/provinces", body, StringComparison.Ordinal);
    }
}
