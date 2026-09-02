using System.Globalization;
using System.Text.Json;
using YaMiSoFt.OpenData.Core.Models;

namespace YaMiSoFt.OpenData.DataTool;

/// <summary>
/// Regenerates <c>data/holidays.json</c>: Turkey's official non-working days, year by year.
/// </summary>
/// <remarks>
/// The dates are precomputed for a range of years and committed rather than calculated at
/// request time. That keeps the API a pure lookup, and — more importantly — makes the
/// religious holidays reviewable: a computed date that turns out to disagree with Diyanet can
/// be corrected in a pull request instead of requiring a code change and a deploy.
///
/// Religious dates come from the Umm al-Qura calendar, which is an astronomical approximation.
/// It has matched the dates Diyanet publishes for every recent year, but Diyanet's calendar
/// and the Resmi Gazete are the authority, not this tool. The overlay in
/// <c>data/overrides/holidays.tr.json</c> exists to correct any year that diverges.
/// </remarks>
public static class HolidaysCommand
{
    private const int DefaultFirstYear = 2015;
    private const int DefaultLastYear = 2050;

    /// <summary>Fixed-date national holidays, as (month, day, Turkish name, English name).</summary>
    private static readonly (int Month, int Day, string NameTr, string NameEn)[] NationalHolidays =
    [
        (1, 1, "Yılbaşı", "New Year's Day"),
        (4, 23, "Ulusal Egemenlik ve Çocuk Bayramı", "National Sovereignty and Children's Day"),
        (5, 1, "Emek ve Dayanışma Günü", "Labour and Solidarity Day"),
        (5, 19, "Atatürk'ü Anma, Gençlik ve Spor Bayramı", "Commemoration of Atatürk, Youth and Sports Day"),
        (7, 15, "Demokrasi ve Millî Birlik Günü", "Democracy and National Unity Day"),
        (8, 30, "Zafer Bayramı", "Victory Day"),
        (10, 29, "Cumhuriyet Bayramı", "Republic Day"),
    ];

    /// <summary>Runs the holiday regeneration. Returns a process exit code.</summary>
    public static async Task<int> RunAsync(IReadOnlyDictionary<string, string> options)
    {
        var outputDirectory = options.GetValueOrDefault("output", "data");
        var firstYear = ReadYear(options, "from", DefaultFirstYear);
        var lastYear = ReadYear(options, "to", DefaultLastYear);

        if (lastYear < firstYear)
        {
            throw new InvalidDataException($"--to ({lastYear}) is before --from ({firstYear}).");
        }

        var overridePath = options.GetValueOrDefault(
            "corrections",
            Path.Combine(outputDirectory, "overrides", "holidays.tr.json"));

        var calendar = new UmAlQuraCalendar();
        var corrections = LoadCorrections(overridePath);
        var holidays = new List<Holiday>();

        for (var year = firstYear; year <= lastYear; year++)
        {
            holidays.AddRange(NationalHolidaysFor(year));
        }

        holidays.AddRange(ReligiousHolidaysFor(firstYear, lastYear, calendar, corrections));

        var ordered = holidays
            .Where(holiday => holiday.Year >= firstYear && holiday.Year <= lastYear)
            .OrderBy(static holiday => holiday.Date)
            .ThenBy(static holiday => holiday.IsHalfDay ? 0 : 1)
            .ThenBy(static holiday => holiday.NameEn, StringComparer.Ordinal)
            .ToArray();

        await DataFileWriter.WriteAsync(
            Path.Combine(outputDirectory, "holidays.json"),
            new DataSet<Holiday>
            {
                Name = "holidays",
                Version = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Source = $"Computed: fixed national dates plus Umm al-Qura religious dates, {firstYear}-{lastYear}",
                License = "MIT",
                Items = ordered,
            }).ConfigureAwait(false);

        Console.WriteLine($"wrote    {ordered.Length} holidays across {lastYear - firstYear + 1} years");
        return 0;
    }

    private static IEnumerable<Holiday> NationalHolidaysFor(int year)
    {
        foreach (var (month, day, nameTr, nameEn) in NationalHolidays)
        {
            yield return new Holiday
            {
                Date = new DateOnly(year, month, day),
                NameTr = nameTr,
                NameEn = nameEn,
                Kind = HolidayKind.National,
                IsHalfDay = false,
            };
        }

        // The afternoon of 28 October is a half day preceding Republic Day.
        yield return new Holiday
        {
            Date = new DateOnly(year, 10, 28),
            NameTr = "Cumhuriyet Bayramı Arifesi",
            NameEn = "Republic Day Eve",
            Kind = HolidayKind.National,
            IsHalfDay = true,
        };
    }

    /// <summary>
    /// Emits the religious holidays whose Gregorian dates fall in the requested range.
    /// </summary>
    /// <remarks>
    /// Hijri years are walked one wider than the Gregorian range in both directions: a Hijri
    /// year straddles two Gregorian ones, so a holiday belonging to the range's first calendar
    /// year can come from the preceding Hijri year. The results are filtered by date afterwards.
    /// </remarks>
    private static IEnumerable<Holiday> ReligiousHolidaysFor(
        int firstYear,
        int lastYear,
        UmAlQuraCalendar calendar,
        IReadOnlyDictionary<int, Corrections> corrections)
    {
        var firstHijri = calendar.GetYear(new DateTime(firstYear, 1, 1)) - 1;
        var lastHijri = calendar.GetYear(new DateTime(lastYear, 12, 31)) + 1;
        var applied = new HashSet<int>();
        var holidays = new List<Holiday>();

        for (var hijriYear = firstHijri; hijriYear <= lastHijri; hijriYear++)
        {
            var correction = corrections.GetValueOrDefault(hijriYear);

            if (correction is not null)
            {
                applied.Add(hijriYear);
            }

            // Ramazan Bayrami runs 1-3 Shawwal; Kurban Bayrami runs 10-13 Dhu al-Hijjah.
            // Each is preceded by a half-day eve.
            var ramazan = correction?.Ramazan ?? ToDate(calendar, hijriYear, 10, 1);
            var kurban = correction?.Kurban ?? ToDate(calendar, hijriYear, 12, 10);

            if (ramazan is { } fitr)
            {
                holidays.AddRange(Festival(fitr, 3, "Ramazan Bayramı", "Ramadan Feast"));
            }

            if (kurban is { } adha)
            {
                holidays.AddRange(Festival(adha, 4, "Kurban Bayramı", "Feast of Sacrifice"));
            }
        }

        var unknown = corrections.Keys.Except(applied).Order().ToArray();

        if (unknown.Length > 0)
        {
            // A correction for a year outside the generated range would silently do nothing.
            throw new InvalidDataException(
                $"Holiday corrections list Hijri year(s) outside the generated range: {string.Join(", ", unknown)}.");
        }

        return holidays;
    }

    private static IEnumerable<Holiday> Festival(DateOnly firstDay, int days, string nameTr, string nameEn)
    {
        yield return new Holiday
        {
            Date = firstDay.AddDays(-1),
            NameTr = $"{nameTr} Arifesi",
            NameEn = $"{nameEn} Eve",
            Kind = HolidayKind.Religious,
            IsHalfDay = true,
        };

        for (var day = 0; day < days; day++)
        {
            yield return new Holiday
            {
                Date = firstDay.AddDays(day),
                NameTr = $"{nameTr} {day + 1}. Gün",
                NameEn = $"{nameEn} Day {day + 1}",
                Kind = HolidayKind.Religious,
                IsHalfDay = false,
            };
        }
    }

    private static DateOnly? ToDate(UmAlQuraCalendar calendar, int hijriYear, int month, int day)
    {
        try
        {
            return DateOnly.FromDateTime(calendar.ToDateTime(hijriYear, month, day, 0, 0, 0, 0));
        }
        catch (ArgumentOutOfRangeException)
        {
            // Umm al-Qura covers roughly 1900-2077; outside that there is nothing to compute.
            return null;
        }
    }

    private static Dictionary<int, Corrections> LoadCorrections(string path)
    {
        var corrections = new Dictionary<int, Corrections>();

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"warn: no holiday corrections at {path}; using computed dates only");
            return corrections;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        if (!document.RootElement.TryGetProperty("religious", out var religious) ||
            religious.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{path}' has no 'religious' object.");
        }

        foreach (var entry in religious.EnumerateObject())
        {
            if (!int.TryParse(entry.Name, CultureInfo.InvariantCulture, out var hijriYear))
            {
                throw new InvalidDataException($"'{path}' has a non-numeric Hijri year key '{entry.Name}'.");
            }

            corrections[hijriYear] = new Corrections(
                ReadDate(entry.Value, "ramazan", path, entry.Name),
                ReadDate(entry.Value, "kurban", path, entry.Name));
        }

        if (corrections.Count > 0)
        {
            Console.WriteLine($"applied  {corrections.Count} curated religious holiday correction(s)");
        }

        return corrections;
    }

    private static DateOnly? ReadDate(JsonElement element, string property, string path, string key)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        if (!DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var date))
        {
            throw new InvalidDataException($"'{path}' has an unparseable {property} date for Hijri year {key}.");
        }

        return date;
    }

    private static int ReadYear(IReadOnlyDictionary<string, string> options, string name, int fallback) =>
        options.TryGetValue(name, out var raw) &&
        int.TryParse(raw, CultureInfo.InvariantCulture, out var year)
            ? year
            : fallback;

    private sealed record Corrections(DateOnly? Ramazan, DateOnly? Kurban);
}
