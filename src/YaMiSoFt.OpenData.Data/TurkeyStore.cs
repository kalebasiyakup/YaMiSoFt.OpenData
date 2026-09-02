using System.Collections.Frozen;
using System.Collections.Immutable;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;
using YaMiSoFt.OpenData.Core.Search;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// In-memory lookup for Turkish provinces and districts (BRD 3.1, Faz 1).
/// </summary>
/// <remarks>
/// Provinces and districts share one store because they are one hierarchy: almost every
/// district query starts from a province, and holding them together lets the province-to-
/// districts edge be a prebuilt frozen lookup rather than a scan of 972 rows per request.
/// </remarks>
public sealed class TurkeyStore
{
    private readonly FrozenDictionary<int, Province> _provincesById;
    private readonly FrozenDictionary<string, Province> _provincesBySlug;
    private readonly FrozenDictionary<int, District> _districtsById;
    private readonly FrozenDictionary<int, ImmutableArray<District>> _districtsByProvince;
    private readonly SearchIndex<Province> _provinceIndex;
    private readonly SearchIndex<District> _districtIndex;

    /// <summary>Builds the store from the loaded province and district datasets.</summary>
    /// <exception cref="InvalidDataException">
    /// Keys are duplicated, or a district points at a province that does not exist.
    /// </exception>
    public TurkeyStore(DataSet<Province> provinces, DataSet<District> districts)
    {
        ArgumentNullException.ThrowIfNull(provinces);
        ArgumentNullException.ThrowIfNull(districts);

        Version = provinces.Version;
        Source = provinces.Source;
        License = provinces.License;

        _provincesById = DataIndex.Build(provinces.Items, static province => province.Id, "province id");
        _provincesBySlug = DataIndex.Build(provinces.Items, static province => province.Slug, "province slug");
        _districtsById = DataIndex.Build(districts.Items, static district => district.Id, "district id");

        var orphans = districts.Items
            .Where(district => !_provincesById.ContainsKey(district.ProvinceId))
            .Select(static district => $"{district.Name} (provinceId {district.ProvinceId})")
            .ToArray();

        if (orphans.Length > 0)
        {
            // Referential integrity is checked here as well as in the tests: a district whose
            // province is missing would be unreachable through the hierarchy and invisible.
            throw new InvalidDataException(
                $"District(s) reference an unknown province: {string.Join(", ", orphans)}.");
        }

        _districtsByProvince = districts.Items
            .GroupBy(static district => district.ProvinceId)
            .ToFrozenDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static district => district.Name, ReferenceOrdering.ComparerFor(Language.Turkish))
                    .ToImmutableArray());

        _provinceIndex = new SearchIndex<Province>(
            provinces.Items,
            static province => [province.Name, province.Slug, province.PlateCode]);

        _districtIndex = new SearchIndex<District>(
            districts.Items,
            static district => [district.Name, district.Slug, district.ProvinceName]);
    }

    /// <summary>Dataset version stamp, used to seed response ETags.</summary>
    public string Version { get; }

    /// <summary>Upstream source of the datasets.</summary>
    public string Source { get; }

    /// <summary>SPDX licence identifier of the datasets.</summary>
    public string License { get; }

    /// <summary>Number of provinces held.</summary>
    public int ProvinceCount => _provinceIndex.Count;

    /// <summary>Number of districts held.</summary>
    public int DistrictCount => _districtIndex.Count;

    /// <summary>Every province, plate code ascending.</summary>
    public IReadOnlyList<Province> AllProvinces => _provinceIndex.Items;

    /// <summary>Every district.</summary>
    public IReadOnlyList<District> AllDistricts => _districtIndex.Items;

    /// <summary>
    /// Resolves a province by plate code or slug. "34", "istanbul" and "İSTANBUL" all work —
    /// the slug index is case-insensitive and slugs are already diacritic-free.
    /// </summary>
    public Province? FindProvince(string? idOrSlug)
    {
        if (string.IsNullOrWhiteSpace(idOrSlug))
        {
            return null;
        }

        var key = idOrSlug.Trim();

        if (int.TryParse(key, out var id) && _provincesById.TryGetValue(id, out var byId))
        {
            return byId;
        }

        // Folded the same way slugs were generated, so a caller can pass the display name
        // ("Kahramanmaraş") and still land on "kahramanmaras".
        return _provincesBySlug.TryGetValue(SearchTextNormalizer.Slugify(key), out var bySlug)
            ? bySlug
            : null;
    }

    /// <summary>Resolves a district by its identifier.</summary>
    public District? FindDistrict(int id) =>
        _districtsById.TryGetValue(id, out var district) ? district : null;

    /// <summary>Returns the districts of a province, ordered by Turkish collation.</summary>
    public IReadOnlyList<District> DistrictsOf(int provinceId) =>
        _districtsByProvince.TryGetValue(provinceId, out var districts) ? districts : [];

    /// <summary>Filters and orders provinces (FR-03, FR-05).</summary>
    /// <param name="search">Raw search term.</param>
    /// <param name="sort">"name", "id" or "districts". Defaults to "name".</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives the collation.</param>
    public IReadOnlyList<Province> QueryProvinces(
        string? search,
        string? sort,
        SortDirection direction,
        Language language)
    {
        var matches = _provinceIndex.Filter(search);

        IOrderedEnumerable<Province> ordered = sort?.ToLowerInvariant() switch
        {
            "id" or "plate" => ReferenceOrdering.ByNumber(matches, static province => province.Id, direction),
            "districts" => ReferenceOrdering.ByNumber(matches, static province => province.DistrictCount, direction),
            _ => ReferenceOrdering.ByText(matches, static province => province.Name, direction, language),
        };

        return [.. ordered];
    }

    /// <summary>Filters and orders districts, optionally scoped to one province.</summary>
    public IReadOnlyList<District> QueryDistricts(
        string? search,
        string? sort,
        SortDirection direction,
        Language language,
        int? provinceId = null)
    {
        var matches = _districtIndex.Filter(search);

        if (provinceId is { } scope)
        {
            matches = matches.Where(district => district.ProvinceId == scope);
        }

        IOrderedEnumerable<District> ordered = sort?.ToLowerInvariant() switch
        {
            "id" => ReferenceOrdering.ByNumber(matches, static district => district.Id, direction),
            "province" => ReferenceOrdering.ByNumber(matches, static district => district.ProvinceId, direction),
            "quarters" => ReferenceOrdering.ByNumber(matches, static district => district.QuarterCount, direction),
            _ => ReferenceOrdering.ByText(matches, static district => district.Name, direction, language),
        };

        return [.. ordered];
    }
}
