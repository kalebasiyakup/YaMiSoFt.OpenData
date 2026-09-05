using System.Net;
using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>Covers the mobile operator endpoints (BRD 5.2).</summary>
public sealed class PhoneEndpointTests(OpenDataApiFactory factory) : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Mobile_operator_resolves_by_its_slug()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/mobile-operators/turkcell", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var op = await ReadJsonAsync(response);
        Assert.Equal("turkcell", op.GetProperty("code").GetString());
        Assert.Equal("Turkcell", op.GetProperty("name").GetString());
        Assert.Contains("İletişim", op.GetProperty("legalName").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Slug_is_case_insensitive()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/mobile-operators/VODAFONE", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var op = await ReadJsonAsync(response);
        Assert.Equal("vodafone", op.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_operator_returns_problem_details()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/mobile-operators/does-not-exist", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Contains("mobile-operator-not-found", problem.GetProperty("type").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Downloading_all_operators_returns_all_three()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/mobile-operators/all", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).EnumerateArray().ToArray();

        Assert.Equal(3, items.Length);
        Assert.Contains(items, item => item.GetProperty("code").GetString() == "turk-telekom");
    }

    [Fact]
    public async Task Operators_are_searchable_by_legal_name()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/mobile-operators?search=telekomünikasyon", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(2, items.Length); // Türk Telekom and Vodafone are both "... Telekomünikasyon A.Ş."
    }

    [Fact]
    public async Task Unknown_field_is_rejected()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/mobile-operators?fields=nope", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
