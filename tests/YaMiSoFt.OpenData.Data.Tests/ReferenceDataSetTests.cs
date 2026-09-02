using System.Text.Json;
using System.Text.RegularExpressions;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Data.Tests;

/// <summary>
/// Integrity gate for the currency and language datasets (PLAN.md 4).
/// </summary>
public sealed class ReferenceDataSetTests
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly DataSet<Currency> Currencies = DataSetLoader.LoadCurrencies(DataDirectory);
    private static readonly DataSet<LanguageInfo> Languages = DataSetLoader.LoadLanguages(DataDirectory);

    [Fact]
    public void Currency_codes_are_unique_iso_4217()
    {
        var invalid = Currencies.Items
            .Where(static currency => !Regex.IsMatch(currency.Code, "^[A-Z]{3}$"))
            .Select(static currency => currency.Code)
            .ToArray();

        Assert.True(invalid.Length == 0, $"Malformed currency codes: {string.Join(", ", invalid)}");

        var duplicates = Currencies.Items
            .GroupBy(static currency => currency.Code, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate currency codes: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Minor_units_are_plausible_and_correct_for_the_known_exceptions()
    {
        var invalid = Currencies.Items
            .Where(static currency => currency.DecimalDigits is < 0 or > 4)
            .Select(static currency => $"{currency.Code}:{currency.DecimalDigits}")
            .ToArray();

        Assert.True(invalid.Length == 0, $"Implausible minor units: {string.Join(", ", invalid)}");

        // The three shapes that exist. Getting these wrong is a rounding bug in every caller
        // that formats money, so they are pinned rather than assumed.
        Assert.Equal(2, Find("TRY").DecimalDigits);
        Assert.Equal(0, Find("JPY").DecimalDigits);
        Assert.Equal(3, Find("KWD").DecimalDigits);
    }

    [Fact]
    public void Curated_turkish_names_are_applied()
    {
        Assert.Equal("Türk Lirası", Find("TRY").NameTr);
        Assert.Equal("ABD Doları", Find("USD").NameTr);
        Assert.Equal("İngiliz Sterlini", Find("GBP").NameTr);
        Assert.Equal("Japon Yeni", Find("JPY").NameTr);
    }

    [Fact]
    public void Turkish_name_overlay_only_lists_currencies_that_exist()
    {
        // The overlay is hand-maintained, so it can drift as upstream changes. The data tool
        // fails on a stale entry; this asserts the same thing from the committed output, so a
        // hand-edited overlay cannot slip through without regenerating.
        var overlayPath = Path.Combine(RepositoryRoot(), "data", "overrides", "currencies.tr.json");
        using var document = JsonDocument.Parse(File.ReadAllText(overlayPath));

        var codes = Currencies.Items.Select(static currency => currency.Code).ToHashSet(StringComparer.Ordinal);

        var unknown = document.RootElement
            .GetProperty("names")
            .EnumerateObject()
            .Select(static entry => entry.Name)
            .Where(name => !codes.Contains(name))
            .ToArray();

        Assert.True(unknown.Length == 0, $"Overlay lists unknown currencies: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Every_currency_has_both_names()
    {
        var missing = Currencies.Items
            .Where(static currency =>
                string.IsNullOrWhiteSpace(currency.NameEn) || string.IsNullOrWhiteSpace(currency.NameTr))
            .Select(static currency => currency.Code)
            .ToArray();

        Assert.True(missing.Length == 0, $"Missing names: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Language_codes_are_well_formed_and_unique()
    {
        var invalid = Languages.Items
            .Where(static language =>
                !Regex.IsMatch(language.Alpha2, "^[a-z]{2}$") ||
                !Regex.IsMatch(language.Alpha3, "^[a-z]{3}$"))
            .Select(static language => $"{language.Alpha2}/{language.Alpha3}")
            .ToArray();

        Assert.True(invalid.Length == 0, $"Malformed language codes: {string.Join(", ", invalid)}");

        var duplicates = Languages.Items
            .GroupBy(static language => language.Alpha2, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate language codes: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Languages_carry_turkish_names_from_icu()
    {
        var turkish = Languages.Items.Single(static language => language.Alpha2 == "tr");
        var english = Languages.Items.Single(static language => language.Alpha2 == "en");
        var german = Languages.Items.Single(static language => language.Alpha2 == "de");

        Assert.Equal("Türkçe", turkish.NameTr);
        Assert.Equal("İngilizce", english.NameTr);
        Assert.Equal("Almanca", german.NameTr);

        // Unlike currencies, ICU can localize language names, so coverage should be near total.
        var untranslated = Languages.Items.Count(static language => language.NameTr == language.NameEn);
        Assert.True(untranslated < 20, $"{untranslated} languages fell back to their English name.");
    }

    [Fact]
    public void Datasets_carry_provenance()
    {
        foreach (var (name, version, source, license) in new[]
        {
            (Currencies.Name, Currencies.Version, Currencies.Source, Currencies.License),
            (Languages.Name, Languages.Version, Languages.Source, Languages.License),
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", version);
            Assert.False(string.IsNullOrWhiteSpace(source));
            Assert.False(string.IsNullOrWhiteSpace(license));
        }
    }

    private static Currency Find(string code) =>
        Currencies.Items.Single(currency => currency.Code == code);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "data", "overrides")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output.");
    }
}
