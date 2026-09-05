using System.Collections.Frozen;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>In-memory ISO 3166-1 country lookup (BRD 3.1, Faz 2).</summary>
public sealed class CountryStore : IReferenceStore<Country>
{
    private readonly FrozenDictionary<string, Country> _byIso2;
    private readonly FrozenDictionary<string, Country> _byIso3;
    private readonly SearchIndex<Country> _index;

    /// <summary>Builds the store from a loaded dataset.</summary>
    /// <exception cref="InvalidDataException">The dataset contains duplicate ISO codes.</exception>
    public CountryStore(DataSet<Country> dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        Version = dataset.Version;
        Source = dataset.Source;
        License = dataset.License;

        _byIso2 = DataIndex.Build(dataset.Items, static country => country.Iso2, "country ISO 3166-1 alpha-2");
        _byIso3 = DataIndex.Build(dataset.Items, static country => country.Iso3, "country ISO 3166-1 alpha-3");
        _index = new SearchIndex<Country>(
            dataset.Items,
            static country => [country.NameEn, country.NameTr, country.Iso2, country.Iso3, country.CallingCode]);
    }

    /// <inheritdoc />
    public string Version { get; }

    /// <summary>Upstream source of the dataset.</summary>
    public string Source { get; }

    /// <summary>SPDX licence identifier of the dataset.</summary>
    public string License { get; }

    /// <summary>Number of countries held.</summary>
    public int Count => _index.Count;

    /// <summary>Every country, dataset order.</summary>
    public IReadOnlyList<Country> All => _index.Items;

    /// <summary>Resolves a country by ISO 3166-1 alpha-2 or alpha-3 code, case-insensitively.</summary>
    public Country? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var key = code.Trim();

        if (_byIso2.TryGetValue(key, out var byIso2))
        {
            return byIso2;
        }

        return _byIso3.TryGetValue(key, out var byIso3) ? byIso3 : null;
    }

    /// <summary>Filters and orders countries.</summary>
    /// <param name="search">Raw search term. Also matches the calling code (e.g. "90").</param>
    /// <param name="sort">"name" or "code". Defaults to "name".</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives the name sorted on and the collation.</param>
    public IReadOnlyList<Country> Query(
        string? search,
        string? sort,
        SortDirection direction,
        Language language)
    {
        var matches = _index.Filter(search);

        IOrderedEnumerable<Country> ordered = sort?.ToLowerInvariant() switch
        {
            "code" => ReferenceOrdering.ByText(matches, static country => country.Iso2, direction, Language.English),
            _ => ReferenceOrdering.ByText(matches, country => NameFor(country, language), direction, language),
        };

        return [.. ordered];
    }

    /// <summary>Returns the localized display name for <paramref name="country"/> (FR-04).</summary>
    public static string NameFor(Country country, Language language)
    {
        ArgumentNullException.ThrowIfNull(country);

        return language == Language.Turkish ? country.NameTr : country.NameEn;
    }
}
