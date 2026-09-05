using System.Net;
using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>Covers the province and district endpoints (BRD 5.2, FR-06).</summary>
public sealed class TurkeyEndpointTests(OpenDataApiFactory factory)
    : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    // FR-06 plus the convenience of resolving the display name, so a caller holding a name
    // from a dropdown does not have to slugify it themselves.
    [InlineData("34")]
    [InlineData("istanbul")]
    [InlineData("İstanbul")]
    [InlineData("ISTANBUL")]
    public async Task Province_resolves_by_plate_code_slug_or_name(string key)
    {
        using var response = await _client.GetAsync(
            new Uri($"/api/v1/provinces/{Uri.EscapeDataString(key)}", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var province = await ReadJsonAsync(response);
        Assert.Equal(34, province.GetProperty("id").GetInt32());
        Assert.Equal("İstanbul", province.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Province_carries_the_fields_the_brd_asks_for()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/provinces/46", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var province = await ReadJsonAsync(response);
        Assert.Equal("Kahramanmaraş", province.GetProperty("name").GetString());
        Assert.Equal("KAHRAMANMARAŞ", province.GetProperty("nameUpper").GetString());
        Assert.Equal("46", province.GetProperty("plateCode").GetString());
        Assert.Equal("kahramanmaras", province.GetProperty("slug").GetString());
        Assert.True(province.GetProperty("districtCount").GetInt32() > 0);
        Assert.Equal(["344"], province.GetProperty("areaCodes").EnumerateArray().Select(static c => c.GetString()));
    }

    [Fact]
    public async Task Istanbul_carries_both_of_its_area_codes()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/provinces/34", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var province = await ReadJsonAsync(response);
        Assert.Equal(
            ["212", "216"],
            province.GetProperty("areaCodes").EnumerateArray().Select(static c => c.GetString()));
    }

    [Fact]
    public async Task All_eighty_one_provinces_are_listed()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/provinces?pageSize=100", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.Equal(81, payload.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Provinces_sort_by_district_count()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/provinces?sort=districts&order=desc&pageSize=3&fields=id,districtCount", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();

        // İstanbul has more districts than any other province, and the ordering is strict.
        Assert.Equal(34, items[0].GetProperty("id").GetInt32());
        Assert.True(items[0].GetProperty("districtCount").GetInt32() >
                    items[1].GetProperty("districtCount").GetInt32());
    }

    [Fact]
    public async Task Districts_of_a_province_are_returned_in_full()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/provinces/34/districts?pageSize=100", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.Equal(39, payload.GetProperty("totalCount").GetInt32());

        var names = payload.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("Beşiktaş", names);
        Assert.Contains("Kadıköy", names);
    }

    [Fact]
    public async Task Districts_of_an_unknown_province_are_a_404_not_an_empty_list()
    {
        // An empty list would read as "this province has no districts", which is never true.
        using var response = await _client.GetAsync(
            new Uri("/api/v1/provinces/999/districts", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Contains("province-not-found", problem.GetProperty("type").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("beşiktaş")]
    [InlineData("besiktas")]
    [InlineData("BESIKTAS")]
    public async Task District_search_folds_turkish_characters(string term)
    {
        using var response = await _client.GetAsync(
            new Uri($"/api/v1/districts?search={Uri.EscapeDataString(term)}", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(items, item => item.GetProperty("name").GetString() == "Beşiktaş");
    }

    [Fact]
    public async Task District_carries_its_province_name()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/districts?search=kadikoy&fields=name,provinceId,provinceName", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var item = (await ReadJsonAsync(response)).GetProperty("items")[0];

        Assert.Equal(34, item.GetProperty("provinceId").GetInt32());
        Assert.Equal("İstanbul", item.GetProperty("provinceName").GetString());
    }

    [Fact]
    public async Task Unknown_district_id_returns_problem_details()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/districts/999999", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Bulk_downloads_return_arrays()
    {
        foreach (var route in new[] { "/api/v1/provinces/all", "/api/v1/districts/all" })
        {
            using var response = await _client.GetAsync(new Uri(route, UriKind.Relative));

            response.EnsureSuccessStatusCode();

            var payload = await ReadJsonAsync(response);
            Assert.Equal(JsonValueKind.Array, payload.ValueKind);
            Assert.True(payload.GetArrayLength() >= 81);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
