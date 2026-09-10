using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>
/// Covers the generated OpenAPI documents' tag structure: per-topic tags rather than the old
/// "Turkey"/"Reference" split, English in "v1" and Turkish in "v1-tr", and the document-level
/// "tags" order that drives Scalar's flat sidebar.
/// </summary>
public sealed class OpenApiDocumentTests(OpenDataApiFactory factory) : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(
        "/openapi/v1.json",
        new[] { "Address", "Mobile Operators", "Countries", "Currencies", "Languages", "Public Holidays", "Banks", "Validation" })]
    [InlineData(
        "/openapi/v1-tr.json",
        new[] { "Adres", "GSM Operatörleri", "Ülke Kodları", "Para Birimleri", "Diller", "Resmi Tatiller", "Bankalar", "Doğrulama" })]
    public async Task Every_operation_tag_is_from_the_expected_set(string route, string[] expectedTags)
    {
        var document = await ReadDocumentAsync(route);
        var expected = expectedTags.ToHashSet(StringComparer.Ordinal);

        var operationTags = document.GetProperty("paths").EnumerateObject()
            .SelectMany(static path => path.Value.EnumerateObject())
            .SelectMany(static operation => operation.Value.GetProperty("tags").EnumerateArray())
            .Select(static tag => tag.GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expected, operationTags);

        // "Turkey" and "Reference" must be gone from both documents, not merely outnumbered.
        Assert.DoesNotContain("Turkey", operationTags);
        Assert.DoesNotContain("Reference", operationTags);
    }

    [Theory]
    [InlineData("/openapi/v1.json", new[] { "Address", "Mobile Operators", "Countries", "Currencies", "Languages", "Public Holidays", "Banks", "Validation" })]
    [InlineData("/openapi/v1-tr.json", new[] { "Adres", "GSM Operatörleri", "Ülke Kodları", "Para Birimleri", "Diller", "Resmi Tatiller", "Bankalar", "Doğrulama" })]
    public async Task Document_level_tags_match_the_operations_that_use_them(string route, string[] expectedTags)
    {
        var document = await ReadDocumentAsync(route);

        var declaredTags = document.GetProperty("tags").EnumerateArray()
            .Select(static tag => tag.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expectedTags.ToHashSet(StringComparer.Ordinal), declaredTags);
    }

    [Theory]
    [InlineData("/openapi/v1.json", new[] { "Address", "Mobile Operators", "Countries", "Currencies", "Languages", "Public Holidays", "Banks", "Validation" })]
    [InlineData("/openapi/v1-tr.json", new[] { "Adres", "GSM Operatörleri", "Ülke Kodları", "Para Birimleri", "Diller", "Resmi Tatiller", "Bankalar", "Doğrulama" })]
    public async Task Document_tags_are_declared_in_sidebar_order(string route, string[] expectedOrder)
    {
        // Scalar's flat sidebar follows the document's own "tags" array order (no
        // x-tagGroups — see TagOrderDocumentTransformer's remarks on why grouping was
        // dropped). TagOrderDocumentTransformer's whole job is putting that array in this
        // order rather than whatever order the endpoints happened to be mapped in.
        var document = await ReadDocumentAsync(route);

        var declaredOrder = document.GetProperty("tags").EnumerateArray()
            .Select(static tag => tag.GetProperty("name").GetString()!)
            .ToArray();

        Assert.Equal(expectedOrder, declaredOrder);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/openapi/v1-tr.json")]
    public async Task No_tag_groups_extension_is_emitted(string route)
    {
        // Every tag in this API is its own top-level section — none share a parent topic — so
        // wrapping each one in a singleton x-tagGroups entry only added a redundant nested
        // menu item with the same label as its parent (confirmed in Scalar's rendered
        // sidebar). Asserting the extension's absence keeps that regression from creeping
        // back in.
        var document = await ReadDocumentAsync(route);

        Assert.False(document.TryGetProperty("x-tagGroups", out _));
    }

    private async Task<JsonElement> ReadDocumentAsync(string route)
    {
        using var response = await _client.GetAsync(new Uri(route, UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
