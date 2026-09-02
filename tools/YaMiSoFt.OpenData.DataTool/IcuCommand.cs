using System.Globalization;
using System.Text.Json;
using YaMiSoFt.OpenData.Core.Models;

namespace YaMiSoFt.OpenData.DataTool;

/// <summary>
/// Regenerates <c>data/currencies.json</c> and <c>data/languages.json</c> from the runtime's
/// own ICU tables.
/// </summary>
/// <remarks>
/// These two datasets need no upstream download at all: .NET ships ICU, which already carries
/// ISO 4217 codes with their symbols and minor-unit counts, and ISO 639 codes with names in
/// any locale. Deriving them here means no third-party licence to track, no network fetch
/// that can rot the way restcountries did during Faz 0, and names that stay in step with the
/// runtime's own culture data.
/// </remarks>
public static class IcuCommand
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Runs the currency regeneration. Returns a process exit code.</summary>
    public static async Task<int> RunCurrenciesAsync(IReadOnlyDictionary<string, string> options)
    {
        var outputDirectory = options.GetValueOrDefault("output", "data");
        var overridePath = options.GetValueOrDefault(
            "tr-names",
            Path.Combine(outputDirectory, "overrides", "currencies.tr.json"));

        var currencies = ApplyTurkishNames(BuildCurrencies(), LoadTurkishNames(overridePath));

        await DataFileWriter.WriteAsync(
            Path.Combine(outputDirectory, "currencies.json"),
            new DataSet<Currency>
            {
                Name = "currencies",
                Version = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Source = $"ICU via .NET {Environment.Version} (ISO 4217)",
                License = "Unicode-3.0",
                Items = currencies,
            }).ConfigureAwait(false);

        Console.WriteLine($"wrote    {currencies.Length} currencies");
        return 0;
    }

    /// <summary>Runs the language regeneration. Returns a process exit code.</summary>
    public static async Task<int> RunLanguagesAsync(IReadOnlyDictionary<string, string> options)
    {
        var outputDirectory = options.GetValueOrDefault("output", "data");
        var languages = BuildLanguages();

        await DataFileWriter.WriteAsync(
            Path.Combine(outputDirectory, "languages.json"),
            new DataSet<LanguageInfo>
            {
                Name = "languages",
                Version = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Source = $"ICU via .NET {Environment.Version} (ISO 639-1/639-2)",
                License = "Unicode-3.0",
                Items = languages,
            }).ConfigureAwait(false);

        Console.WriteLine($"wrote    {languages.Length} languages");
        return 0;
    }

    private static Currency[] BuildCurrencies()
    {
        var byCode = new Dictionary<string, Currency>(StringComparer.Ordinal);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;

            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                // Not every specific culture maps to a region ICU knows.
                continue;
            }

            var code = region.ISOCurrencySymbol;

            if (code.Length != 3 || byCode.ContainsKey(code))
            {
                continue;
            }

            byCode[code] = new Currency
            {
                Code = code,
                NameEn = region.CurrencyEnglishName,
                // Placeholder: ICU exposes a currency's name only in English and in its own
                // home locale, never in an arbitrary target language. The curated overlay in
                // data/overrides/ replaces this for the currencies that have a Turkish name.
                NameTr = region.CurrencyEnglishName,
                Symbol = string.IsNullOrWhiteSpace(region.CurrencySymbol) ? null : region.CurrencySymbol,
                DecimalDigits = culture.NumberFormat.CurrencyDecimalDigits,
            };
        }

        return [.. byCode.Values.OrderBy(static currency => currency.Code, StringComparer.Ordinal)];
    }

    private static LanguageInfo[] BuildLanguages()
    {
        var byAlpha2 = new Dictionary<string, LanguageInfo>(StringComparer.Ordinal);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (culture.Name.Length == 0)
            {
                continue;
            }

            var alpha2 = culture.TwoLetterISOLanguageName;
            var alpha3 = culture.ThreeLetterISOLanguageName;

            // Neutral cultures include script and region variants ("zh-Hans"); only the plain
            // ISO 639-1 entries belong in a language list.
            if (alpha2.Length != 2 || alpha3.Length != 3 || byAlpha2.ContainsKey(alpha2))
            {
                continue;
            }

            byAlpha2[alpha2] = new LanguageInfo
            {
                Alpha2 = alpha2,
                Alpha3 = alpha3,
                NameEn = culture.EnglishName,
                NameTr = TurkishNameOf(culture),
                NativeName = string.IsNullOrWhiteSpace(culture.NativeName) ? null : culture.NativeName,
            };
        }

        return [.. byAlpha2.Values.OrderBy(static language => language.Alpha2, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Reads a language's name in Turkish. <see cref="CultureInfo.DisplayName"/> resolves
    /// against the current UI culture, so it is swapped for the duration of the read.
    /// </summary>
    private static string TurkishNameOf(CultureInfo culture)
    {
        var previous = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = TurkishCulture;
            var name = culture.DisplayName;

            return string.IsNullOrWhiteSpace(name) ? culture.EnglishName : name;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// Reads the curated Turkish name overlay. A missing file is not an error: the generated
    /// dataset is still valid, it just carries English names throughout.
    /// </summary>
    private static IReadOnlyDictionary<string, string> LoadTurkishNames(string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"warn: no Turkish name overlay at {path}; using English names");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        if (!document.RootElement.TryGetProperty("names", out var names) ||
            names.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{path}' has no 'names' object.");
        }

        return names.EnumerateObject()
            .Where(static entry => entry.Value.ValueKind == JsonValueKind.String)
            .ToDictionary(
                static entry => entry.Name,
                static entry => entry.Value.GetString()!,
                StringComparer.Ordinal);
    }

    private static Currency[] ApplyTurkishNames(
        Currency[] currencies,
        IReadOnlyDictionary<string, string> turkishNames)
    {
        var unknown = turkishNames.Keys
            .Except(currencies.Select(static currency => currency.Code), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (unknown.Length > 0)
        {
            // A code in the overlay that no longer exists upstream means the overlay has gone
            // stale; surface it here rather than letting it sit unnoticed forever.
            throw new InvalidDataException(
                $"Turkish name overlay lists unknown currency code(s): {string.Join(", ", unknown)}.");
        }

        Console.WriteLine($"applied  {turkishNames.Count} curated Turkish currency names");

        return
        [
            .. currencies.Select(currency => turkishNames.TryGetValue(currency.Code, out var name)
                ? currency with { NameTr = name }
                : currency),
        ];
    }
}
