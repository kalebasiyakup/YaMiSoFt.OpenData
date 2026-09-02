using System.Collections.Frozen;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>In-memory ISO 4217 currency lookup (BRD 3.1, Faz 1).</summary>
public sealed class CurrencyStore : IReferenceStore<Currency>
{
    private readonly FrozenDictionary<string, Currency> _byCode;
    private readonly SearchIndex<Currency> _index;

    /// <summary>Builds the store from a loaded dataset.</summary>
    /// <exception cref="InvalidDataException">The dataset contains duplicate codes.</exception>
    public CurrencyStore(DataSet<Currency> dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        Version = dataset.Version;
        Source = dataset.Source;
        License = dataset.License;

        _byCode = DataIndex.Build(dataset.Items, static currency => currency.Code, "currency code");
        _index = new SearchIndex<Currency>(
            dataset.Items,
            static currency => [currency.NameEn, currency.NameTr, currency.Code]);
    }

    /// <inheritdoc />
    public string Version { get; }

    /// <summary>Upstream source of the dataset.</summary>
    public string Source { get; }

    /// <summary>SPDX licence identifier of the dataset.</summary>
    public string License { get; }

    /// <summary>Number of currencies held.</summary>
    public int Count => _index.Count;

    /// <summary>Every currency, code ascending.</summary>
    public IReadOnlyList<Currency> All => _index.Items;

    /// <summary>Resolves a currency by ISO 4217 code, case-insensitively.</summary>
    public Currency? Find(string? code) =>
        !string.IsNullOrWhiteSpace(code) && _byCode.TryGetValue(code.Trim(), out var currency)
            ? currency
            : null;

    /// <summary>Filters and orders currencies.</summary>
    /// <param name="search">Raw search term.</param>
    /// <param name="sort">"name" or "code". Defaults to "code" — an ISO list reads best that way.</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives the name sorted on and the collation.</param>
    public IReadOnlyList<Currency> Query(
        string? search,
        string? sort,
        SortDirection direction,
        Language language)
    {
        var matches = _index.Filter(search);

        IOrderedEnumerable<Currency> ordered = sort?.ToLowerInvariant() switch
        {
            "name" => ReferenceOrdering.ByText(matches, currency => NameFor(currency, language), direction, language),
            _ => ReferenceOrdering.ByText(matches, static currency => currency.Code, direction, Language.English),
        };

        return [.. ordered];
    }

    /// <summary>Returns the localized display name for <paramref name="currency"/> (FR-04).</summary>
    public static string NameFor(Currency currency, Language language)
    {
        ArgumentNullException.ThrowIfNull(currency);

        return language == Language.Turkish ? currency.NameTr : currency.NameEn;
    }
}
