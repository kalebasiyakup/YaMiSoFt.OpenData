using System.Collections.Frozen;
using System.Collections.Immutable;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// In-memory lookup for Turkish quarters (semt) and, through them, postal codes.
/// </summary>
/// <remarks>
/// This is the store that makes postal code lookup cheap. The code is assigned per quarter,
/// so answering "which place is 34357?" needs 2,433 rows, not the 73,552-row settlement file
/// — the difference between a lookup that is always in memory and one that would drag a
/// lazy-loaded dataset in on a cold start.
///
/// The postal code index is built with <see cref="DataIndex"/>, which rejects duplicates. That
/// is deliberate: the endpoint returns a single quarter for a code, so a data file that ever
/// assigned one code to two quarters must fail at startup rather than silently answer with
/// whichever row won.
/// </remarks>
public sealed class QuarterStore
{
    private readonly FrozenDictionary<int, Quarter> _byId;
    private readonly FrozenDictionary<string, Quarter> _byPostalCode;
    private readonly FrozenDictionary<int, ImmutableArray<Quarter>> _byDistrict;
    private readonly SearchIndex<Quarter> _index;

    /// <summary>Builds the store from the loaded quarter dataset.</summary>
    /// <exception cref="InvalidDataException">
    /// Identifiers or postal codes are duplicated, or a quarter points at a district that does
    /// not exist.
    /// </exception>
    public QuarterStore(DataSet<Quarter> quarters, TurkeyStore turkey)
    {
        ArgumentNullException.ThrowIfNull(quarters);
        ArgumentNullException.ThrowIfNull(turkey);

        Version = quarters.Version;

        _byId = DataIndex.Build(quarters.Items, static quarter => quarter.Id, "quarter id");
        _byPostalCode = DataIndex.Build(quarters.Items, static quarter => quarter.PostalCode, "postal code");

        var orphans = quarters.Items
            .Where(quarter => turkey.FindDistrict(quarter.DistrictId) is null)
            .Select(static quarter => $"{quarter.Name} (districtId {quarter.DistrictId})")
            .Take(5)
            .ToArray();

        if (orphans.Length > 0)
        {
            throw new InvalidDataException(
                $"Quarter(s) reference an unknown district: {string.Join(", ", orphans)}.");
        }

        _byDistrict = quarters.Items
            .GroupBy(static quarter => quarter.DistrictId)
            .ToFrozenDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static quarter => quarter.Name, ReferenceOrdering.ComparerFor(Language.Turkish))
                    .ToImmutableArray());

        _index = new SearchIndex<Quarter>(
            quarters.Items,
            static quarter => [quarter.Name, quarter.Slug, quarter.PostalCode, quarter.DistrictName]);
    }

    /// <summary>Dataset version stamp, used to seed response ETags.</summary>
    public string Version { get; }

    /// <summary>Number of quarters held.</summary>
    public int Count => _index.Count;

    /// <summary>Every quarter.</summary>
    public IReadOnlyList<Quarter> All => _index.Items;

    /// <summary>Resolves a quarter by its identifier.</summary>
    public Quarter? Find(int id) => _byId.TryGetValue(id, out var quarter) ? quarter : null;

    /// <summary>Resolves the single quarter a postal code belongs to.</summary>
    public Quarter? FindByPostalCode(string? postalCode) =>
        !string.IsNullOrWhiteSpace(postalCode) &&
        _byPostalCode.TryGetValue(postalCode.Trim(), out var quarter)
            ? quarter
            : null;

    /// <summary>Returns the quarters of one district, ordered by Turkish collation.</summary>
    public IReadOnlyList<Quarter> OfDistrict(int districtId) =>
        _byDistrict.TryGetValue(districtId, out var quarters) ? quarters : [];

    /// <summary>Filters and orders quarters, optionally scoped to a province or district.</summary>
    /// <param name="search">Raw search term, matched on name, slug, postal code and district.</param>
    /// <param name="sort">"name", "id", "postalcode" or "settlements". Defaults to "name".</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives the collation.</param>
    /// <param name="provinceId">Restricts to one province.</param>
    /// <param name="districtId">Restricts to one district.</param>
    public IReadOnlyList<Quarter> Query(
        string? search,
        string? sort,
        SortDirection direction,
        Language language,
        int? provinceId = null,
        int? districtId = null)
    {
        var matches = _index.Filter(search);

        if (provinceId is { } province)
        {
            matches = matches.Where(quarter => quarter.ProvinceId == province);
        }

        if (districtId is { } district)
        {
            matches = matches.Where(quarter => quarter.DistrictId == district);
        }

        IOrderedEnumerable<Quarter> ordered = sort?.ToLowerInvariant() switch
        {
            "id" => ReferenceOrdering.ByNumber(matches, static quarter => quarter.Id, direction),
            // Postal codes are digits in a fixed-width string, so an ordinal sort is a numeric
            // sort; using the Turkish collation here would be slower and no more correct.
            "postalcode" => ReferenceOrdering.ByText(
                matches, static quarter => quarter.PostalCode, direction, Language.English),
            "settlements" => ReferenceOrdering.ByNumber(
                matches, static quarter => quarter.SettlementCount, direction),
            _ => ReferenceOrdering.ByText(matches, static quarter => quarter.Name, direction, language),
        };

        return [.. ordered];
    }
}
