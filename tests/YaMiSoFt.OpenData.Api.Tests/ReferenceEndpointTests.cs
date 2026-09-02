using System.Net;
using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>Covers the currency and language endpoints (BRD 5.2).</summary>
public sealed class ReferenceEndpointTests(OpenDataApiFactory factory)
    : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Currency_carries_symbol_and_minor_units()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/currencies/TRY", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var currency = await ReadJsonAsync(response);
        Assert.Equal("TRY", currency.GetProperty("code").GetString());
        Assert.Equal("Türk Lirası", currency.GetProperty("nameTr").GetString());
        Assert.Equal("₺", currency.GetProperty("symbol").GetString());
        Assert.Equal(2, currency.GetProperty("decimalDigits").GetInt32());
    }

    [Theory]
    [InlineData("JPY", 0)]
    [InlineData("KWD", 3)]
    [InlineData("USD", 2)]
    public async Task Minor_units_reflect_the_real_currency(string code, int expected)
    {
        using var response = await _client.GetAsync(new Uri($"/api/v1/currencies/{code}", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var currency = await ReadJsonAsync(response);
        Assert.Equal(expected, currency.GetProperty("decimalDigits").GetInt32());
    }

    [Fact]
    public async Task Currency_code_is_case_insensitive()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/currencies/try", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var currency = await ReadJsonAsync(response);
        Assert.Equal("TRY", currency.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_currency_returns_problem_details()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/currencies/ZZZ", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Contains("currency-not-found", problem.GetProperty("type").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tr")]
    [InlineData("tur")]
    [InlineData("TR")]
    public async Task Language_resolves_by_either_iso_code(string code)
    {
        using var response = await _client.GetAsync(new Uri($"/api/v1/languages/{code}", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var language = await ReadJsonAsync(response);
        Assert.Equal("tr", language.GetProperty("alpha2").GetString());
        Assert.Equal("tur", language.GetProperty("alpha3").GetString());
        Assert.Equal("Türkçe", language.GetProperty("nativeName").GetString());
    }

    [Fact]
    public async Task Languages_are_searchable_by_their_turkish_name()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/languages?search=almanca", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(items, item => item.GetProperty("alpha2").GetString() == "de");
    }

    [Fact]
    public async Task Currencies_are_searchable_by_their_turkish_name()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/currencies?search=sterlin", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(items, item => item.GetProperty("code").GetString() == "GBP");
    }

    [Fact]
    public async Task Field_selection_works_across_the_new_endpoints()
    {
        foreach (var (route, expected) in new[]
        {
            ("/api/v1/currencies?fields=code,symbol&pageSize=2", 2),
            ("/api/v1/languages?fields=alpha2&pageSize=2", 1),
        })
        {
            using var response = await _client.GetAsync(new Uri(route, UriKind.Relative));

            response.EnsureSuccessStatusCode();

            foreach (var item in (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray())
            {
                Assert.Equal(expected, item.EnumerateObject().Count());
            }
        }
    }

    [Fact]
    public async Task Unknown_field_is_rejected_on_the_new_endpoints()
    {
        // The shared parser means every list endpoint validates identically; this pins that.
        foreach (var route in new[]
        {
            "/api/v1/provinces?fields=nope",
            "/api/v1/districts?fields=nope",
            "/api/v1/currencies?fields=nope",
            "/api/v1/languages?fields=nope",
        })
        {
            using var response = await _client.GetAsync(new Uri(route, UriKind.Relative));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Page_size_limit_applies_to_every_list_endpoint()
    {
        foreach (var route in new[]
        {
            "/api/v1/provinces?pageSize=501",
            "/api/v1/districts?pageSize=501",
            "/api/v1/currencies?pageSize=501",
            "/api/v1/languages?pageSize=501",
        })
        {
            using var response = await _client.GetAsync(new Uri(route, UriKind.Relative));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
