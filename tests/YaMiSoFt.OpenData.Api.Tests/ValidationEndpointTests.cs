using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>Covers the IBAN and T.C. Kimlik No validation endpoints (BRD §3.1 Faz 3).</summary>
public sealed class ValidationEndpointTests(OpenDataApiFactory factory) : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Valid_iban_returns_true_with_no_reason()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/validate/iban/TR330006100519786457841326", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync(response);
        Assert.True(result.GetProperty("isValid").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("reason").ValueKind);
        Assert.Equal("TR330006100519786457841326", result.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Invalid_iban_returns_false_with_a_reason_and_still_200s()
    {
        // A validator answering "no" is a successful response, not a client error.
        using var response = await _client.GetAsync(
            new Uri("/api/v1/validate/iban/TR330006100519786457841327", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync(response);
        Assert.False(result.GetProperty("isValid").GetBoolean());
        Assert.Equal("invalid-checksum", result.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Iban_with_spaces_and_lower_case_still_validates()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/validate/iban/tr33%200006%201005%201978%206457%208413%2026", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync(response);
        Assert.True(result.GetProperty("isValid").GetBoolean());
        Assert.Equal("TR330006100519786457841326", result.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Valid_tc_kimlik_returns_true_with_no_reason()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/validate/tc-kimlik/10000000146", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync(response);
        Assert.True(result.GetProperty("isValid").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("reason").ValueKind);
    }

    [Theory]
    [InlineData("10000000147", "invalid-checksum")]
    [InlineData("1234567890", "invalid-format")]
    public async Task Invalid_tc_kimlik_returns_false_with_a_reason(string no, string expectedReason)
    {
        using var response = await _client.GetAsync(
            new Uri($"/api/v1/validate/tc-kimlik/{no}", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync(response);
        Assert.False(result.GetProperty("isValid").GetBoolean());
        Assert.Equal(expectedReason, result.GetProperty("reason").GetString());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
