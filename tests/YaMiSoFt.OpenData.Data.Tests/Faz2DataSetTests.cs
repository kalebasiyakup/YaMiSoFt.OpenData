using System.Text.RegularExpressions;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Data.Tests;

/// <summary>
/// Integrity gate for the Faz 2 datasets: holidays, settlements and mobile prefixes
/// (PLAN.md 4).
/// </summary>
public sealed class Faz2DataSetTests
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly DataSet<Holiday> Holidays = DataSetLoader.LoadHolidays(DataDirectory);
    private static readonly DataSet<Neighborhood> Settlements = DataSetLoader.LoadNeighborhoods(DataDirectory);
    private static readonly DataSet<MobilePrefix> MobilePrefixes = DataSetLoader.LoadMobilePrefixes(DataDirectory);

    [Fact]
    public void Every_year_has_the_seven_fixed_national_holidays()
    {
        var years = Holidays.Items.Select(static holiday => holiday.Year).Distinct().Order().ToArray();

        foreach (var year in years)
        {
            var national = Holidays.Items
                .Where(holiday => holiday.Year == year && holiday.Kind == HolidayKind.National && !holiday.IsHalfDay)
                .ToArray();

            Assert.True(national.Length == 7, $"{year} has {national.Length} full national holidays, expected 7.");
        }
    }

    [Fact]
    public void Religious_holidays_match_the_dates_turkey_published()
    {
        // Anchors taken from the official calendar. If a regeneration moves one of these, the
        // computed calendar has diverged from Diyanet and needs a curated correction.
        AssertHoliday(new DateOnly(2026, 3, 20), "Ramadan Feast Day 1");
        AssertHoliday(new DateOnly(2026, 5, 27), "Feast of Sacrifice Day 1");
        AssertHoliday(new DateOnly(2025, 3, 30), "Ramadan Feast Day 1");
        AssertHoliday(new DateOnly(2025, 6, 6), "Feast of Sacrifice Day 1");
        AssertHoliday(new DateOnly(2024, 4, 10), "Ramadan Feast Day 1");
        AssertHoliday(new DateOnly(2024, 6, 16), "Feast of Sacrifice Day 1");
    }

    [Fact]
    public void Festivals_run_the_right_number_of_days_with_a_half_day_eve()
    {
        var ramazan2026 = Holidays.Items
            .Where(static holiday => holiday.Year == 2026 && holiday.NameEn.StartsWith("Ramadan Feast", StringComparison.Ordinal))
            .OrderBy(static holiday => holiday.Date)
            .ToArray();

        Assert.Equal(4, ramazan2026.Length);
        Assert.True(ramazan2026[0].IsHalfDay);
        Assert.All(ramazan2026[1..], holiday => Assert.False(holiday.IsHalfDay));

        var kurban2026 = Holidays.Items
            .Where(static holiday => holiday.Year == 2026 && holiday.NameEn.StartsWith("Feast of Sacrifice", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(5, kurban2026.Length);
    }

    [Fact]
    public void Settlement_identifiers_are_unique()
    {
        var duplicates = Settlements.Items
            .GroupBy(static settlement => settlement.Id)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .Take(5)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate settlement ids: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Settlements_reference_the_hierarchy_above_them_consistently()
    {
        var quarters = DataSetLoader.LoadQuarters(DataDirectory).Items
            .ToDictionary(static quarter => quarter.Id);

        // One check rather than three: a settlement is placed by the whole chain, and a row
        // naming a real quarter but the wrong district would file it under a district that
        // does not contain it — served without error, and wrong.
        var broken = Settlements.Items
            .Where(settlement => !quarters.TryGetValue(settlement.QuarterId, out var quarter) ||
                                 quarter.DistrictId != settlement.DistrictId ||
                                 quarter.ProvinceId != settlement.ProvinceId)
            .Select(static settlement =>
                $"{settlement.Name}: quarter {settlement.QuarterId}, district {settlement.DistrictId}, province {settlement.ProvinceId}")
            .Take(5)
            .ToArray();

        Assert.True(broken.Length == 0, $"Settlement hierarchy broken: {string.Join("; ", broken)}");
    }

    [Fact]
    public void Postal_codes_agree_with_the_quarter_they_are_copied_from()
    {
        // The code is denormalized onto every settlement so a response reads as a full address
        // line. Denormalized values are only safe while they are checked against their source.
        var quarterCodes = DataSetLoader.LoadQuarters(DataDirectory).Items
            .ToDictionary(static quarter => quarter.Id, static quarter => quarter.PostalCode);

        var stale = Settlements.Items
            .Where(settlement => quarterCodes.GetValueOrDefault(settlement.QuarterId) != settlement.PostalCode)
            .Select(static settlement => $"{settlement.Name}: {settlement.PostalCode}")
            .Take(5)
            .ToArray();

        Assert.True(stale.Length == 0, $"Stale postal codes: {string.Join(", ", stale)}");
    }

    [Fact]
    public void Kind_agrees_with_what_the_name_says()
    {
        // The kind is derived from the name, so the two can only disagree if the derivation
        // rule changed without the file being regenerated. "X Mah (Y Köyü)" is the shape that
        // makes this worth asserting: it names a village and is still a neighbourhood.
        var wrong = Settlements.Items
            .Where(static settlement => settlement.Kind == SettlementKind.Village &&
                                        settlement.VillageName is not null)
            .Select(static settlement => settlement.Name)
            .Concat(Settlements.Items
                .Where(static settlement => settlement.Kind == SettlementKind.Neighborhood &&
                                            !settlement.Name.Contains("Mah", StringComparison.OrdinalIgnoreCase))
                .Select(static settlement => settlement.Name))
            .Take(5)
            .ToArray();

        Assert.True(wrong.Length == 0, $"Kind disagrees with the name: {string.Join(", ", wrong)}");

        // Both kinds are present in meaningful numbers; a rule that collapsed one into the
        // other would still pass every check above.
        var villages = Settlements.Items.Count(static settlement => settlement.Kind == SettlementKind.Village);
        Assert.InRange(villages, 10_000, 20_000);
        Assert.InRange(Settlements.Items.Count - villages, 50_000, 70_000);
    }

    [Fact]
    public void Rural_neighbourhoods_name_the_village_they_sit_in()
    {
        var rural = Settlements.Items.Where(static settlement => settlement.VillageName is not null).ToArray();

        Assert.NotEmpty(rural);

        var wrong = rural
            .Where(static settlement => !settlement.Name.Contains(settlement.VillageName!, StringComparison.Ordinal))
            .Select(static settlement => $"{settlement.Name} -> {settlement.VillageName}")
            .Take(5)
            .ToArray();

        Assert.True(wrong.Length == 0, $"Village name not taken from the settlement name: {string.Join(", ", wrong)}");
    }

    [Fact]
    public void Mobile_prefixes_are_three_digits_starting_with_five()
    {
        Assert.NotEmpty(MobilePrefixes.Items);

        foreach (var prefix in MobilePrefixes.Items)
        {
            Assert.Matches("^5[0-9]{2}$", prefix.Prefix);
            Assert.Equal("90" + prefix.Prefix, prefix.International);
        }

        // The best-known Turkish mobile prefixes; their absence would mean the pattern was
        // parsed wrongly rather than that the allocation changed.
        var prefixes = MobilePrefixes.Items.Select(static p => p.Prefix).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("532", prefixes);
        Assert.Contains("505", prefixes);
        Assert.Contains("542", prefixes);
    }

    private static void AssertHoliday(DateOnly date, string nameEn) =>
        Assert.True(
            Holidays.Items.Any(holiday => holiday.Date == date && holiday.NameEn == nameEn),
            $"Expected '{nameEn}' on {date:yyyy-MM-dd}.");
}
