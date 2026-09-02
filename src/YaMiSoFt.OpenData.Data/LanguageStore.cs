using System.Collections.Frozen;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>In-memory ISO 639 language lookup (BRD 3.1, Faz 1).</summary>
public sealed class LanguageStore : IReferenceStore<LanguageInfo>
{
    private readonly FrozenDictionary<string, LanguageInfo> _byAlpha2;
    private readonly FrozenDictionary<string, LanguageInfo> _byAlpha3;
    private readonly SearchIndex<LanguageInfo> _index;

    /// <summary>Builds the store from a loaded dataset.</summary>
    /// <exception cref="InvalidDataException">The dataset contains duplicate codes.</exception>
    public LanguageStore(DataSet<LanguageInfo> dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        Version = dataset.Version;
        Source = dataset.Source;
        License = dataset.License;

        _byAlpha2 = DataIndex.Build(dataset.Items, static language => language.Alpha2, "language alpha-2");
        _byAlpha3 = DataIndex.Build(dataset.Items, static language => language.Alpha3, "language alpha-3");
        _index = new SearchIndex<LanguageInfo>(
            dataset.Items,
            static language => [language.NameEn, language.NameTr, language.NativeName, language.Alpha2, language.Alpha3]);
    }

    /// <inheritdoc />
    public string Version { get; }

    /// <summary>Upstream source of the dataset.</summary>
    public string Source { get; }

    /// <summary>SPDX licence identifier of the dataset.</summary>
    public string License { get; }

    /// <summary>Number of languages held.</summary>
    public int Count => _index.Count;

    /// <summary>Every language, alpha-2 ascending.</summary>
    public IReadOnlyList<LanguageInfo> All => _index.Items;

    /// <summary>Resolves a language by ISO 639-1 or 639-2 code, case-insensitively.</summary>
    public LanguageInfo? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var key = code.Trim();

        if (_byAlpha2.TryGetValue(key, out var byAlpha2))
        {
            return byAlpha2;
        }

        return _byAlpha3.TryGetValue(key, out var byAlpha3) ? byAlpha3 : null;
    }

    /// <summary>Filters and orders languages.</summary>
    /// <param name="search">Raw search term.</param>
    /// <param name="sort">"name" or "code". Defaults to "name".</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives the name sorted on and the collation.</param>
    public IReadOnlyList<LanguageInfo> Query(
        string? search,
        string? sort,
        SortDirection direction,
        Language language)
    {
        var matches = _index.Filter(search);

        IOrderedEnumerable<LanguageInfo> ordered = sort?.ToLowerInvariant() switch
        {
            "code" => ReferenceOrdering.ByText(matches, static item => item.Alpha2, direction, Language.English),
            _ => ReferenceOrdering.ByText(matches, item => NameFor(item, language), direction, language),
        };

        return [.. ordered];
    }

    /// <summary>Returns the localized display name for <paramref name="language"/> (FR-04).</summary>
    public static string NameFor(LanguageInfo language, Language responseLanguage)
    {
        ArgumentNullException.ThrowIfNull(language);

        return responseLanguage == Language.Turkish ? language.NameTr : language.NameEn;
    }
}
