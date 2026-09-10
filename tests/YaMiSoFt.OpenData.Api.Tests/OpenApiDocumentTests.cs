using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>
/// Covers the generated OpenAPI documents' tag structure: per-topic tags rather than the old
/// "Turkey"/"Reference" split, English in "v1" and Turkish in "v1-tr", and the "x-tagGroups"
/// extension that drives Scalar's sidebar sections.
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
        // A document's own "tags" declarations drifting from what operations actually carry
        // would leave x-tagGroups naming tags the document doesn't otherwise know about.
        var document = await ReadDocumentAsync(route);

        var declaredTags = document.GetProperty("tags").EnumerateArray()
            .Select(static tag => tag.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expectedTags.ToHashSet(StringComparer.Ordinal), declaredTags);
    }

    [Theory]
    [InlineData("/openapi/v1.json", "Countries", "Mobile Operators")]
    [InlineData("/openapi/v1-tr.json", "Ülke Kodları", "GSM Operatörleri")]
    public async Task Country_codes_are_a_top_level_section_of_their_own(
        string route, string countryTag, string operatorTag)
    {
        // Country codes used to sit under a "Phone"/"Telefon" heading next to the mobile
        // operators. They are a reference dataset callers reach for on their own, so each of
        // the two is now its own section rather than one being nested beside the other.
        var document = await ReadDocumentAsync(route);
        var groups = document.GetProperty("x-tagGroups").EnumerateArray().ToArray();

        var countryGroup = Assert.Single(
            groups, group => group.GetProperty("name").GetString() == countryTag);
        Assert.Equal([countryTag], countryGroup.GetProperty("tags").EnumerateArray().Select(static t => t.GetString()));

        var operatorGroup = Assert.Single(
            groups, group => group.GetProperty("name").GetString() == operatorTag);
        Assert.Equal([operatorTag], operatorGroup.GetProperty("tags").EnumerateArray().Select(static t => t.GetString()));
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
