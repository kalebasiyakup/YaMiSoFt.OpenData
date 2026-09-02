using System.Net;
using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>
/// Covers the quarter (semt) endpoints — the address level that carries the postal code
/// (BRD 5.2).
/// </summary>
public sealed class QuarterEndpointTests(OpenDataApiFactory factory)
    : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Quarter_reads_standalone()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/quarters/1154", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        // The point of the denormalized names: an address form can render this row without
        // three further requests up the hierarchy.
        var quarter = await ReadJsonAsync(response);
        Assert.Equal("Abbasağa", quarter.GetProperty("name").GetString());
        Assert.Equal("34022", quarter.GetProperty("postalCode").GetString());
        Assert.Equal(34, quarter.GetProperty("provinceId").GetInt32());
        Assert.Equal("İstanbul", quarter.GetProperty("provinceName").GetString());
        Assert.Equal(430, quarter.GetProperty("districtId").GetInt32());
        Assert.Equal("Beşiktaş", quarter.GetProperty("districtName").GetString());
    }

    [Fact]
    public async Task Quarters_of_a_district_are_returned_in_full()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/districts/430/quarters?pageSize=100", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.Equal(10, payload.GetProperty("totalCount").GetInt32());

        var names = payload.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("Abbasağa", names);
        Assert.Contains("Bebek", names);
    }

    [Fact]
    public async Task Quarters_of_an_unknown_district_are_a_404_not_an_empty_list()
    {
        // An empty list would read as "this district has no quarters", which is never true.
        using var response = await _client.GetAsync(
            new Uri("/api/v1/districts/999999/quarters", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Contains("district-not-found", problem.GetProperty("type").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quarter_lists_its_own_settlements()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/quarters/1154/neighborhoods?fields=name,quarterId", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.Equal(3, payload.GetProperty("totalCount").GetInt32());
        Assert.All(
            payload.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(1154, item.GetProperty("quarterId").GetInt32()));
    }

    [Fact]
    public async Task Unknown_quarter_returns_problem_details()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/quarters/999999", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("türkali")]
    [InlineData("turkali")]
    [InlineData("TURKALI")]
    public async Task Quarter_search_folds_turkish_characters(string term)
    {
        using var response = await _client.GetAsync(
            new Uri($"/api/v1/quarters?search={Uri.EscapeDataString(term)}&fields=name", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, item => item.GetProperty("name").GetString() == "Türkali");
    }

    [Fact]
    public async Task Quarters_are_searchable_by_postal_code()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/quarters?search=34357&fields=postalCode", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, item => item.GetProperty("postalCode").GetString() == "34357");
    }

    [Fact]
    public async Task Quarters_can_be_narrowed_to_one_province()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/quarters?provinceId=34&pageSize=500&fields=provinceId", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("totalCount").GetInt32() > 0);
        Assert.All(
            payload.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(34, item.GetProperty("provinceId").GetInt32()));
    }

    [Fact]
    public async Task Bulk_download_returns_every_quarter()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/quarters/all", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, payload.ValueKind);
        Assert.Equal(2433, payload.GetArrayLength());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
