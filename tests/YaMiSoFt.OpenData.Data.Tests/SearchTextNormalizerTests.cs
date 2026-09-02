using YaMiSoFt.OpenData.Core.Search;

namespace YaMiSoFt.OpenData.Data.Tests;

/// <summary>
/// Covers FR-05: search must be case- and diacritic-insensitive, including the Turkish
/// letters that Unicode decomposition alone does not handle.
/// </summary>
public sealed class SearchTextNormalizerTests
{
    [Theory]
    // The requirement's own example.
    [InlineData("İstanbul", "istanbul")]
    [InlineData("istanbul", "istanbul")]
    [InlineData("ISTANBUL", "istanbul")]
    [InlineData("IstanbuI", "istanbui")]
    // Dotless i: no Unicode decomposition exists, so this is the case a naive
    // FormD-and-strip-marks implementation gets wrong.
    [InlineData("Işık", "isik")]
    [InlineData("ısı", "isi")]
    // Letters that do decompose.
    [InlineData("Çanakkale", "canakkale")]
    [InlineData("Şanlıurfa", "sanliurfa")]
    [InlineData("Ğğ", "gg")]
    [InlineData("Öö Üü", "oo uu")]
    // Non-Turkish diacritics still fold.
    [InlineData("Åland", "aland")]
    [InlineData("Côte d'Ivoire", "cote d'ivoire")]
    [InlineData("Curaçao", "curacao")]
    // Whitespace handling.
    [InlineData("  Kahramanmaraş  ", "kahramanmaras")]
    [InlineData("Bosna   Hersek", "bosna hersek")]
    public void Normalize_folds_case_and_diacritics(string input, string expected) =>
        Assert.Equal(expected, SearchTextNormalizer.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_returns_empty_for_blank_input(string? input) =>
        Assert.Equal(string.Empty, SearchTextNormalizer.Normalize(input));

    [Fact]
    public void Turkish_variants_of_the_same_word_share_one_key()
    {
        var variants = new[] { "İSVİÇRE", "isviçre", "İsviçre", "isvicre", "ISVICRE" };

        var keys = variants.Select(SearchTextNormalizer.Normalize).Distinct(StringComparer.Ordinal);

        Assert.Single(keys);
    }

    [Fact]
    public void Matches_treats_an_empty_term_as_matching_everything() =>
        Assert.True(SearchTextNormalizer.Matches("anything", string.Empty));
}
