using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>
/// Covers the generated OpenAPI documents' tag structure: per-topic tags rather than the old
/// "Turkey"/"Reference" split, English in "v1" and Turkish in "v1-tr", and the "x-tagGroups"
/// extension Scalar's sidebar nests "Phone"/"Telefon" under.
/// </summary>
public sealed class OpenApiDocumentTests(OpenDataApiFactory factory) : IClassFixture<OpenDataApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(
        "/openapi/v1.json",
        new[] { "Address", "Mobile Operators", "Countries", "Currencies", "Languages", "Public Holidays", "Validation" })]
    [InlineData(
        "/openapi/v1-tr.json",
        new[] { "Adres", "GSM Operatörleri", "Ülke Kodları", "Para Birimleri", "Diller", "Resmi Tatiller", "Doğrulama" })]
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
    [InlineData("/openapi/v1.json", new[] { "Address", "Mobile Operators", "Countries", "Currencies", "Languages", "Public Holidays", "Validation" })]
    [InlineData("/openapi/v1-tr.json", new[] { "Adres", "GSM Operatörleri", "Ülke Kodları", "Para Birimleri", "Diller", "Resmi Tatiller", "Doğrulama" })]
    public async Task Document_level_tags_match_the_operations_that_use_them(string route, string[] expectedTags)
    {
        // A document's own "tags" declarations drifting from what operations actually carry
        // would leave x-tagGroups naming tags the document doesn't otherwise know about.
        var document = await ReadDocumentAsync(route);

        var declaredTags = document.GetProperty("tags").EnumerateArray()
            .Select(static tag => tag.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expectedTags.ToHashSet(StringComparer.Ordinal), declaredTags);
    }

    [Fact]
    public async Task English_document_nests_mobile_operators_and_countries_under_phone()
    {
        var document = await ReadDocumentAsync("/openapi/v1.json");

        var phoneGroup = document.GetProperty("x-tagGroups").EnumerateArray()
            .Single(static group => group.GetProperty("name").GetString() == "Phone");

        var tags = phoneGroup.GetProperty("tags").EnumerateArray().Select(static t => t.GetString());

        Assert.Equal(["Mobile Operators", "Countries"], tags);
    }

    [Fact]
    public async Task Turkish_document_nests_gsm_and_country_codes_under_telefon()
    {
        var document = await ReadDocumentAsync("/openapi/v1-tr.json");

        var phoneGroup = document.GetProperty("x-tagGroups").EnumerateArray()
            .Single(static group => group.GetProperty("name").GetString() == "Telefon");

        var tags = phoneGroup.GetProperty("tags").EnumerateArray().Select(static t => t.GetString());

        Assert.Equal(["GSM Operatörleri", "Ülke Kodları"], tags);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/openapi/v1-tr.json")]
    public async Task Every_tag_belongs_to_exactly_one_group(string route)
    {
        // Whether Scalar shows a tag that appears in no x-tagGroups entry at all is
        // undocumented; every tag is placed in exactly one group (most as a singleton) so
        // nothing can silently vanish from the sidebar.
        var document = await ReadDocumentAsync(route);

        var declaredTags = document.GetProperty("tags").EnumerateArray()
            .Select(static tag => tag.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var groupedTags = document.GetProperty("x-tagGroups").EnumerateArray()
            .SelectMany(static group => group.GetProperty("tags").EnumerateArray())
            .Select(static tag => tag.GetString()!)
            .ToArray();

        Assert.Equal(declaredTags.Count, groupedTags.Length);
        Assert.Equal(declaredTags, groupedTags.ToHashSet(StringComparer.Ordinal));
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
