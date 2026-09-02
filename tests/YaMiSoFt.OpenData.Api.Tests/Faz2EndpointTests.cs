using System.Net;
using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>Covers the Faz 2 endpoints: holidays, settlements, quarters, postal codes.</summary>
public sealed class Faz2EndpointTests(OpenDataApiFactory factory)
    : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Holidays_for_a_year_include_both_kinds()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/holidays/2026", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).EnumerateArray().ToArray();

        Assert.Contains(items, item => item.GetProperty("kind").GetString() == "National");
        Assert.Contains(items, item => item.GetProperty("kind").GetString() == "Religious");
        Assert.Contains(items, item => item.GetProperty("date").GetString() == "2026-10-29");
        Assert.Contains(items, item => item.GetProperty("date").GetString() == "2026-03-20");
    }

    [Fact]
    public async Task Enums_are_serialized_as_names()
    {
        // A numeric kind tells a caller nothing and shifts meaning if a member is inserted.
        using var response = await _client.GetAsync(new Uri("/api/v1/holidays/2026", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"kind\":\"National\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"kind\":0", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Year_outside_the_covered_range_is_a_400_naming_the_range()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/holidays/2099", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Contains("year-out-of-range", problem.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.Contains("2050", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsupported_country_is_rejected_rather_than_ignored()
    {
        // Silently returning Turkish holidays for a German request would be undetectable.
        using var response = await _client.GetAsync(new Uri("/api/v1/holidays/2026?country=DE", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task District_settlements_are_listed()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/districts/430/neighborhoods?pageSize=100&fields=name,postalCode", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("totalCount").GetInt32() > 0);

        var names = payload.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()).ToArray();

        Assert.Contains("Abbasağa Mah", names);
    }

    [Fact]
    public async Task Settlements_of_an_unknown_district_are_a_404()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/districts/999999/neighborhoods", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unnarrowed_settlement_list_is_refused()
    {
        // 73,552 rows paged 500 at a time is the scraping pattern the bulk endpoints exist to
        // remove elsewhere; here the answer is to narrow the request.
        using var response = await _client.GetAsync(new Uri("/api/v1/neighborhoods", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Contains("provinceId", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("village")]
    [InlineData("koy")]
    public async Task Settlements_filter_by_kind(string kind)
    {
        using var response = await _client.GetAsync(
            new Uri($"/api/v1/neighborhoods?provinceId=2&kind={kind}&fields=kind&pageSize=5", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        foreach (var item in (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray())
        {
            Assert.Equal("Village", item.GetProperty("kind").GetString());
        }
    }

    [Fact]
    public async Task Unknown_kind_is_rejected()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/neighborhoods?provinceId=2&kind=city", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Postal_code_resolves_to_the_quarter_it_is_assigned_to()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/postal-codes/34357?fields=name,districtId,districtName", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        // One code, one quarter: the answer is an object, not a list to pick from.
        var quarter = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Object, quarter.ValueKind);
        Assert.Equal("Türkali", quarter.GetProperty("name").GetString());
        Assert.Equal(430, quarter.GetProperty("districtId").GetInt32());
        Assert.Equal("Beşiktaş", quarter.GetProperty("districtName").GetString());
    }

    [Fact]
    public async Task Postal_code_lists_the_settlements_it_covers()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/postal-codes/34357/neighborhoods?fields=name,postalCode", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("totalCount").GetInt32() > 0);
        Assert.All(
            payload.GetProperty("items").EnumerateArray(),
            item => Assert.Equal("34357", item.GetProperty("postalCode").GetString()));
    }

    [Fact]
    public async Task Unknown_postal_code_returns_problem_details()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/postal-codes/99999", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Settlement_search_folds_turkish_characters()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/neighborhoods?search=abbasaga&fields=name&pageSize=5", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.True(payload.GetProperty("totalCount").GetInt32() > 0);
        Assert.Contains(
            payload.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("name").GetString() == "Abbasağa Mah");
    }

    [Fact]
    public async Task Settlements_can_be_narrowed_to_one_quarter()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/neighborhoods?quarterId=1154&fields=name,quarterId", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var payload = await ReadJsonAsync(response);
        Assert.Equal(3, payload.GetProperty("totalCount").GetInt32());
        Assert.All(
            payload.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(1154, item.GetProperty("quarterId").GetInt32()));
    }

    [Fact]
    public async Task Readiness_does_not_wait_for_the_lazy_dataset()
    {
        // Settlements load on first use; a pod must be ready before that happens.
        using var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
