using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Search;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Data.Tests;

/// <summary>
/// Integrity gate for the Turkish address hierarchy: provinces, districts and quarters
/// (PLAN.md 4).
/// </summary>
/// <remarks>
/// The four files are generated together from one source, so these tests are less about
/// catching upstream drift than about catching a partial regeneration — a district file
/// refreshed while the quarter file that points into it was not. Every assertion here is one
/// that a half-regenerated hierarchy would fail.
/// </remarks>
public sealed class TurkeyDataSetTests
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly DataSet<Province> Provinces = DataSetLoader.LoadProvinces(DataDirectory);
    private static readonly DataSet<District> Districts = DataSetLoader.LoadDistricts(DataDirectory);
    private static readonly DataSet<Quarter> Quarters = DataSetLoader.LoadQuarters(DataDirectory);

    [Fact]
    public void There_are_exactly_eighty_one_provinces()
    {
        // Turkey has had 81 provinces since 1999. A different number means the data is wrong,
        // not that the country changed — and if it ever does change, this test is where the
        // decision to accept that gets made deliberately.
        Assert.Equal(81, Provinces.Items.Count);
    }

    [Fact]
    public void Plate_codes_cover_one_to_eighty_one_exactly()
    {
        var codes = Provinces.Items.Select(static province => province.Id).Order().ToArray();

        Assert.Equal(Enumerable.Range(1, 81), codes);
    }

    [Fact]
    public void Plate_code_string_is_the_zero_padded_identifier()
    {
        var wrong = Provinces.Items
            .Where(static province => province.PlateCode != province.Id.ToString("00", null))
            .Select(static province => $"{province.Name}: {province.PlateCode} vs {province.Id}")
            .ToArray();

        Assert.True(wrong.Length == 0, $"Plate code mismatch: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void Province_slugs_are_unique_url_safe_and_ascii()
    {
        var duplicates = Provinces.Items
            .GroupBy(static province => province.Slug, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate slugs: {string.Join(", ", duplicates)}");

        // Slugs go in URLs; a stray Turkish character there would need percent-encoding and
        // defeat the point of having a slug at all.
        var invalid = Provinces.Items
            .Where(static province => !province.Slug.All(static c => char.IsAsciiLetterLower(c) || c == '-'))
            .Select(static province => $"{province.Id}:{province.Slug}")
            .ToArray();

        Assert.True(invalid.Length == 0, $"Non-ASCII or unsafe slugs: {string.Join(", ", invalid)}");
    }

    [Fact]
    public void Every_slug_is_what_the_normalizer_produces_from_the_name()
    {
        // The generator and the province lookup both go through Slugify. If a slug in the file
        // were ever hand-edited away from that, /provinces/{name} would stop resolving it.
        var wrong = Provinces.Items
            .Where(static province => province.Slug != SearchTextNormalizer.Slugify(province.Name))
            .Select(static province => $"{province.Name} -> {province.Slug}")
            .Concat(Districts.Items
                .Where(static district => district.Slug != SearchTextNormalizer.Slugify(district.Name))
                .Select(static district => $"{district.Name} -> {district.Slug}"))
            .Concat(Quarters.Items
                .Where(static quarter => quarter.Slug != SearchTextNormalizer.Slugify(quarter.Name))
                .Select(static quarter => $"{quarter.Name} -> {quarter.Slug}"))
            .Take(5)
            .ToArray();

        Assert.True(wrong.Length == 0, $"Slug does not match the name: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void Every_district_belongs_to_a_real_province()
    {
        var plateCodes = Provinces.Items.Select(static province => province.Id).ToHashSet();

        var orphans = Districts.Items
            .Where(district => !plateCodes.Contains(district.ProvinceId))
            .Select(static district => $"{district.Name} -> {district.ProvinceId}")
            .ToArray();

        Assert.True(orphans.Length == 0, $"Districts with no province: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void Every_quarter_belongs_to_a_real_district_in_the_right_province()
    {
        var provinceOfDistrict = Districts.Items.ToDictionary(
            static district => district.Id,
            static district => district.ProvinceId);

        var broken = Quarters.Items
            .Where(quarter => provinceOfDistrict.GetValueOrDefault(quarter.DistrictId, -1) != quarter.ProvinceId)
            .Select(static quarter => $"{quarter.Name}: district {quarter.DistrictId}, province {quarter.ProvinceId}")
            .Take(5)
            .ToArray();

        // Both keys are checked together on purpose: a quarter naming a real district but the
        // wrong province would still serve, and would put the settlement under it in a
        // province it does not belong to.
        Assert.True(broken.Length == 0, $"Quarter hierarchy broken: {string.Join("; ", broken)}");
    }

    [Fact]
    public void Identifiers_are_unique_at_every_level()
    {
        AssertUnique(Districts.Items.Select(static district => district.Id), "district");
        AssertUnique(Quarters.Items.Select(static quarter => quarter.Id), "quarter");
    }

    [Fact]
    public void Postal_codes_are_five_digits_and_unique_to_one_quarter()
    {
        var malformed = Quarters.Items
            .Where(static quarter => quarter.PostalCode.Length != 5 ||
                                     !quarter.PostalCode.All(char.IsAsciiDigit))
            .Select(static quarter => $"{quarter.Name}:{quarter.PostalCode}")
            .Take(5)
            .ToArray();

        Assert.True(malformed.Length == 0, $"Malformed postal codes: {string.Join(", ", malformed)}");

        // Uniqueness is the contract /postal-codes/{code} depends on: it returns one quarter,
        // not a list, so a shared code would make the answer arbitrary.
        AssertUnique(Quarters.Items.Select(static quarter => quarter.PostalCode), "postal code");
    }

    [Fact]
    public void Postal_code_prefix_matches_the_province()
    {
        // The first two digits of a Turkish postal code are the plate code.
        var mismatched = Quarters.Items
            .Where(static quarter => int.Parse(quarter.PostalCode[..2], null) != quarter.ProvinceId)
            .Select(static quarter => $"{quarter.Name}: {quarter.PostalCode} in province {quarter.ProvinceId}")
            .Take(5)
            .ToArray();

        Assert.True(mismatched.Length == 0, $"Postal code/province mismatch: {string.Join("; ", mismatched)}");
    }

    [Fact]
    public void Declared_child_counts_match_the_files_below()
    {
        // Each level stores the size of the level under it, so a regeneration that refreshed
        // one file and not the next shows up here rather than as a wrong count in a response.
        var districtsPerProvince = Districts.Items
            .GroupBy(static district => district.ProvinceId)
            .ToDictionary(static group => group.Key, static group => group.Count());

        var quartersPerDistrict = Quarters.Items
            .GroupBy(static quarter => quarter.DistrictId)
            .ToDictionary(static group => group.Key, static group => group.Count());

        var provinceMismatches = Provinces.Items
            .Where(province => districtsPerProvince.GetValueOrDefault(province.Id) != province.DistrictCount)
            .Select(province => $"{province.Name}: declared {province.DistrictCount}, found {districtsPerProvince.GetValueOrDefault(province.Id)}")
            .ToArray();

        var districtMismatches = Districts.Items
            .Where(district => quartersPerDistrict.GetValueOrDefault(district.Id) != district.QuarterCount)
            .Select(district => $"{district.Name}: declared {district.QuarterCount}, found {quartersPerDistrict.GetValueOrDefault(district.Id)}")
            .Take(5)
            .ToArray();

        Assert.True(provinceMismatches.Length == 0, string.Join("; ", provinceMismatches));
        Assert.True(districtMismatches.Length == 0, string.Join("; ", districtMismatches));
    }

    [Fact]
    public void Denormalized_names_agree_with_the_file_they_came_from()
    {
        var provinceNames = Provinces.Items.ToDictionary(
            static province => province.Id,
            static province => province.Name);

        var districtNames = Districts.Items.ToDictionary(
            static district => district.Id,
            static district => district.Name);

        var stale = Districts.Items
            .Where(district => district.ProvinceName is not null)
            .Where(district => provinceNames.GetValueOrDefault(district.ProvinceId) != district.ProvinceName)
            .Select(static district => $"district {district.Name}: {district.ProvinceName}")
            .Concat(Quarters.Items
                .Where(quarter => quarter.DistrictName is not null)
                .Where(quarter => districtNames.GetValueOrDefault(quarter.DistrictId) != quarter.DistrictName)
                .Select(static quarter => $"quarter {quarter.Name}: {quarter.DistrictName}"))
            .Take(5)
            .ToArray();

        Assert.True(stale.Length == 0, $"Stale denormalized names: {string.Join(", ", stale)}");
    }

    [Fact]
    public void Upper_case_names_are_the_turkish_upper_case_of_the_names()
    {
        var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

        // Not cosmetic: nameUpper exists so a caller without tr-TR casing can display the
        // upper-case form. If it disagreed with name, it would publish a different place.
        var wrong = Provinces.Items
            .Where(province => province.NameUpper != province.Name.ToUpper(turkish))
            .Select(static province => $"{province.Name} vs {province.NameUpper}")
            .Take(5)
            .ToArray();

        Assert.True(wrong.Length == 0, $"Upper-case name mismatch: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void Every_province_has_at_least_one_well_formed_area_code()
    {
        var malformed = Provinces.Items
            .SelectMany(province => province.AreaCodes.Select(code => (province.Name, code)))
            .Where(static entry => entry.code.Length != 3 || !entry.code.All(char.IsAsciiDigit))
            .Select(static entry => $"{entry.Name}:{entry.code}")
            .ToArray();

        Assert.True(malformed.Length == 0, $"Malformed area codes: {string.Join(", ", malformed)}");

        var missing = Provinces.Items
            .Where(static province => province.AreaCodes.Count == 0)
            .Select(static province => province.Name)
            .ToArray();

        Assert.True(missing.Length == 0, $"Provinces with no area code: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Only_istanbul_has_more_than_one_area_code()
    {
        var multi = Provinces.Items
            .Where(static province => province.AreaCodes.Count > 1)
            .Select(static province => province.Name)
            .ToArray();

        Assert.Equal(["İstanbul"], multi);
        Assert.Equal(["212", "216"], Provinces.Items.Single(static p => p.Id == 34).AreaCodes);
    }

    [Fact]
    public void Area_codes_are_unique_across_provinces()
    {
        // 82 codes total: 81 provinces, İstanbul carrying two.
        AssertUnique(Provinces.Items.SelectMany(static province => province.AreaCodes), "area code");
        Assert.Equal(82, Provinces.Items.Sum(static province => province.AreaCodes.Count));
    }

    [Fact]
    public void Store_exposes_the_hierarchy()
    {
        var store = new TurkeyStore(Provinces, Districts);

        Assert.Equal(81, store.ProvinceCount);
        Assert.Equal(972, store.DistrictCount);

        var istanbul = store.FindProvince("34");
        Assert.NotNull(istanbul);
        Assert.Equal("İstanbul", istanbul!.Name);

        // Plate code, slug and the display name all resolve to the same province.
        Assert.Equal(istanbul, store.FindProvince("istanbul"));
        Assert.Equal(istanbul, store.FindProvince("İSTANBUL"));

        Assert.Equal(istanbul.DistrictCount, store.DistrictsOf(istanbul.Id).Count);
    }

    [Fact]
    public void Quarter_store_resolves_postal_codes_without_the_settlement_file()
    {
        var store = new QuarterStore(Quarters, new TurkeyStore(Provinces, Districts));

        Assert.Equal(2433, store.Count);

        var quarter = store.FindByPostalCode("34357");
        Assert.NotNull(quarter);
        Assert.Equal(34, quarter!.ProvinceId);

        // Round trip: the code resolves the quarter, the quarter republishes the code.
        Assert.Equal("34357", quarter.PostalCode);
        Assert.Equal(quarter, store.Find(quarter.Id));
        Assert.Contains(quarter, store.OfDistrict(quarter.DistrictId));
    }

    private static void AssertUnique(IEnumerable<int> keys, string label)
    {
        var duplicates = keys.GroupBy(static key => key)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .Take(5)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate {label} ids: {string.Join(", ", duplicates)}");
    }

    private static void AssertUnique(IEnumerable<string> keys, string label)
    {
        var duplicates = keys.GroupBy(static key => key, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .Take(5)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate {label}s: {string.Join(", ", duplicates)}");
    }
}
