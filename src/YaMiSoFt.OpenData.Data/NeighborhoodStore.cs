using System.Collections.Frozen;
using System.Collections.Immutable;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// In-memory lookup of Turkish neighbourhoods and villages, loaded on first use.
/// </summary>
/// <remarks>
/// This is the one dataset that does not load at startup, and the reason is measured rather
/// than assumed. The memory cost was never the constraint the plan expected; the parse and
/// index pass is, and on a scale-to-zero platform it would land on every cold start.
///
/// Deferring it keeps that cost with the feature that causes it: a request for a country, a
/// province or a postal code still starts in about 300 ms, and only the first request that
/// actually needs settlements pays for them. Subsequent requests hit the loaded store, and
/// the CDN absorbs most of them anyway.
///
/// There is no postal code index here on purpose. Codes belong to quarters, so
/// <see cref="QuarterStore"/> answers a code lookup from 2,433 always-resident rows and this
/// store is only reached for the settlements underneath — which means a postal code query no
/// longer drags the whole dataset into memory.
/// </remarks>
public sealed class NeighborhoodStore
{
    private readonly Lazy<Index> _index;

    /// <summary>Creates a store that will load from <paramref name="directory"/> on first use.</summary>
    public NeighborhoodStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        // LazyThreadSafetyMode.ExecutionAndPublication: several requests can arrive together on
        // a cold instance, and the load must happen once rather than once per request.
        _index = new Lazy<Index>(
            () => new Index(DataSetLoader.LoadNeighborhoods(directory)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>True once the dataset has been loaded.</summary>
    public bool IsLoaded => _index.IsValueCreated;

    /// <summary>Dataset version stamp. Triggers the load.</summary>
    public string Version => _index.Value.Version;

    /// <summary>Number of settlements held. Triggers the load.</summary>
    public int Count => _index.Value.All.Length;

    /// <summary>Every settlement. Triggers the load.</summary>
    public IReadOnlyList<Neighborhood> All => _index.Value.All;

    /// <summary>Resolves a settlement by its identifier.</summary>
    public Neighborhood? Find(int id) =>
        _index.Value.ById.TryGetValue(id, out var settlement) ? settlement : null;

    /// <summary>Returns the settlements of one district, name ascending.</summary>
    public IReadOnlyList<Neighborhood> OfDistrict(int districtId) =>
        _index.Value.ByDistrict.TryGetValue(districtId, out var settlements) ? settlements : [];

    /// <summary>Returns the settlements of one quarter, name ascending.</summary>
    public IReadOnlyList<Neighborhood> OfQuarter(int quarterId) =>
        _index.Value.ByQuarter.TryGetValue(quarterId, out var settlements) ? settlements : [];

    /// <summary>Filters and orders settlements, optionally scoped down the hierarchy.</summary>
    /// <param name="search">Raw search term, matched on name, slug, postal code and village.</param>
    /// <param name="sort">"name", "id" or "postalcode". Defaults to "name".</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives the collation.</param>
    /// <param name="provinceId">Restricts to one province.</param>
    /// <param name="districtId">Restricts to one district.</param>
    /// <param name="quarterId">Restricts to one quarter.</param>
    /// <param name="kind">Restricts to neighbourhoods or villages.</param>
    public IReadOnlyList<Neighborhood> Query(
        string? search,
        string? sort,
        SortDirection direction,
        Language language,
        int? provinceId = null,
        int? districtId = null,
        int? quarterId = null,
        SettlementKind? kind = null)
    {
        var matches = _index.Value.Search.Filter(search);

        if (provinceId is { } province)
        {
            matches = matches.Where(settlement => settlement.ProvinceId == province);
        }

        if (districtId is { } district)
        {
            matches = matches.Where(settlement => settlement.DistrictId == district);
        }

        if (quarterId is { } quarter)
        {
            matches = matches.Where(settlement => settlement.QuarterId == quarter);
        }

        if (kind is { } settlementKind)
        {
            matches = matches.Where(settlement => settlement.Kind == settlementKind);
        }

        IOrderedEnumerable<Neighborhood> ordered = sort?.ToLowerInvariant() switch
        {
            "id" => ReferenceOrdering.ByNumber(matches, static settlement => settlement.Id, direction),
            "postalcode" => ReferenceOrdering.ByText(
                matches, static settlement => settlement.PostalCode, direction, Language.English),
            _ => ReferenceOrdering.ByText(matches, static settlement => settlement.Name, direction, language),
        };

        return [.. ordered];
    }

    /// <summary>The loaded indexes, built once behind the <see cref="Lazy{T}"/>.</summary>
    private sealed class Index
    {
        internal Index(DataSet<Neighborhood> dataset)
        {
            Version = dataset.Version;
            All = [.. dataset.Items];

            ById = DataIndex.Build(dataset.Items, static settlement => settlement.Id, "settlement id");
            ByDistrict = GroupByTurkishName(dataset.Items, static settlement => settlement.DistrictId);
            ByQuarter = GroupByTurkishName(dataset.Items, static settlement => settlement.QuarterId);

            // VillageName is deliberately not indexed: it is a literal substring of Name, so
            // the name key already matches every term it could, and a fourth key across
            // 73,552 rows is memory spent on nothing.
            Search = new SearchIndex<Neighborhood>(
                dataset.Items,
                static settlement => [settlement.Name, settlement.Slug, settlement.PostalCode]);
        }

        internal string Version { get; }

        internal ImmutableArray<Neighborhood> All { get; }

        internal FrozenDictionary<int, Neighborhood> ById { get; }

        internal FrozenDictionary<int, ImmutableArray<Neighborhood>> ByDistrict { get; }

        internal FrozenDictionary<int, ImmutableArray<Neighborhood>> ByQuarter { get; }

        internal SearchIndex<Neighborhood> Search { get; }

        private static FrozenDictionary<int, ImmutableArray<Neighborhood>> GroupByTurkishName(
            IReadOnlyList<Neighborhood> items,
            Func<Neighborhood, int> keySelector) =>
            items
                .GroupBy(keySelector)
                .ToFrozenDictionary(
                    static group => group.Key,
                    group => group
                        .OrderBy(static settlement => settlement.Name, ReferenceOrdering.ComparerFor(Language.Turkish))
                        .ToImmutableArray());
    }
}
