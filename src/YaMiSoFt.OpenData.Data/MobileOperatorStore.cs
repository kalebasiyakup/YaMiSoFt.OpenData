using System.Collections.Frozen;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>In-memory lookup for Turkey's licensed mobile network operators (BRD 3.1, Faz 2).</summary>
public sealed class MobileOperatorStore : IReferenceStore<MobileOperator>
{
    private readonly FrozenDictionary<string, MobileOperator> _byCode;
    private readonly SearchIndex<MobileOperator> _index;

    /// <summary>Builds the store from a loaded dataset.</summary>
    /// <exception cref="InvalidDataException">The dataset contains duplicate codes.</exception>
    public MobileOperatorStore(DataSet<MobileOperator> dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        Version = dataset.Version;
        Source = dataset.Source;
        License = dataset.License;

        _byCode = DataIndex.Build(dataset.Items, static op => op.Code, "mobile operator code");
        _index = new SearchIndex<MobileOperator>(
            dataset.Items,
            static op => [op.Name, op.LegalName, op.Code]);
    }

    /// <inheritdoc />
    public string Version { get; }

    /// <summary>Upstream source of the dataset.</summary>
    public string Source { get; }

    /// <summary>SPDX licence identifier of the dataset.</summary>
    public string License { get; }

    /// <summary>Number of operators held.</summary>
    public int Count => _index.Count;

    /// <summary>Every operator, name ascending.</summary>
    public IReadOnlyList<MobileOperator> All => _index.Items;

    /// <summary>Resolves an operator by its slug code, case-insensitively.</summary>
    public MobileOperator? Find(string? code) =>
        !string.IsNullOrWhiteSpace(code) && _byCode.TryGetValue(code.Trim(), out var op)
            ? op
            : null;

    /// <summary>Filters and orders operators.</summary>
    /// <param name="search">Raw search term.</param>
    /// <param name="sort">"name" or "code". Defaults to "name".</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives collation; the name itself does not vary by language.</param>
    public IReadOnlyList<MobileOperator> Query(
        string? search,
        string? sort,
        SortDirection direction,
        Language language)
    {
        var matches = _index.Filter(search);

        IOrderedEnumerable<MobileOperator> ordered = sort?.ToLowerInvariant() switch
        {
            "code" => ReferenceOrdering.ByText(matches, static op => op.Code, direction, Language.English),
            _ => ReferenceOrdering.ByText(matches, static op => op.Name, direction, language),
        };

        return [.. ordered];
    }
}
