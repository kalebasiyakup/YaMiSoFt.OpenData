using System.Globalization;
using System.Text.Json;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Search;

namespace YaMiSoFt.OpenData.DataTool;

/// <summary>
/// Regenerates the whole Turkish address hierarchy: <c>provinces.json</c>,
/// <c>districts.json</c>, <c>quarters.json</c> and <c>neighborhoods.json</c>.
/// </summary>
/// <remarks>
/// One command rather than four because the four files are one dataset. The identifiers are
/// only meaningful relative to each other — a quarter's <c>districtId</c> means nothing
/// against a district file from a different vintage — so regenerating one without the others
/// would produce a hierarchy that loads and then answers wrongly. Emitting them together
/// makes a partial regeneration impossible rather than merely discouraged.
///
/// The input is the curated Turkish-language export the served files were compiled from. It
/// is read from disk, not fetched: the data is compiled for this project, so there is no
/// upstream URL to go stale and no build-time network dependency.
///
/// The export itself is NOT in the repository — it is 16 MB that would otherwise sit beside
/// the 21 MB it generates. So this command only runs for whoever holds a copy of it; point
/// <c>--source</c> at the directory containing iller/ilceler/semtler/mahalle-ve-koyler. For
/// everyone else the committed <c>data/*.json</c> files are the source of truth, reviewed as
/// diffs like any other data change.
/// </remarks>
public static class TurkeyCommand
{
    /// <summary>
    /// Where the export is looked for when <c>--source</c> is omitted. The directory is not
    /// in the repository; this is only a convenient place to drop a copy.
    /// </summary>
    private const string DefaultSource = "data/source";

    /// <summary>Runs the hierarchy regeneration. Returns a process exit code.</summary>
    public static async Task<int> RunAsync(IReadOnlyDictionary<string, string> options)
    {
        var source = options.GetValueOrDefault("source", DefaultSource).TrimEnd('/', '\\');
        var outputDirectory = options.GetValueOrDefault("output", "data");
        var version = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        const string provenance = "Compiled for this project (data/source)";

        Console.WriteLine($"reading  {source}");

        using var provinceDocument = await ReadAsync(source, "iller").ConfigureAwait(false);
        using var districtDocument = await ReadAsync(source, "ilceler").ConfigureAwait(false);
        using var quarterDocument = await ReadAsync(source, "semtler").ConfigureAwait(false);
        using var settlementDocument = await ReadAsync(source, "mahalle-ve-koyler").ConfigureAwait(false);

        var rawProvinces = Rows(provinceDocument, "Iller");
        var rawDistricts = Rows(districtDocument, "Ilceler");
        var rawQuarters = Rows(quarterDocument, "Semtler");
        var rawSettlements = Rows(settlementDocument, "MahalleVeKoyler");

        // Counted before the records are built so each level can carry the size of the level
        // below it. A caller listing districts to build a dropdown needs to know which of them
        // have quarters without fetching all 2,433.
        var districtsPerProvince = Tally(rawDistricts, "IlId");
        var quartersPerDistrict = Tally(rawQuarters, "IlceId");
        var settlementsPerQuarter = Tally(rawSettlements, "SemtId");

        var provinces = rawProvinces
            .Select(element => MapProvince(element, districtsPerProvince))
            .OrderBy(static province => province.Id)
            .ToArray();

        var provinceNames = provinces.ToDictionary(
            static province => province.Id,
            static province => province.Name);

        var districts = rawDistricts
            .Select(element => MapDistrict(element, provinceNames, quartersPerDistrict))
            .OrderBy(static district => district.ProvinceId)
            .ThenBy(static district => district.Id)
            .ToArray();

        var districtNames = districts.ToDictionary(
            static district => district.Id,
            static district => district.Name);

        var quarters = rawQuarters
            .Select(element => MapQuarter(element, provinceNames, districtNames, settlementsPerQuarter))
            .OrderBy(static quarter => quarter.ProvinceId)
            .ThenBy(static quarter => quarter.DistrictId)
            .ThenBy(static quarter => quarter.Id)
            .ToArray();

        var postalCodes = quarters.ToDictionary(
            static quarter => quarter.Id,
            static quarter => quarter.PostalCode);

        var settlements = rawSettlements
            .Select(element => MapSettlement(element, postalCodes))
            .OrderBy(static settlement => settlement.ProvinceId)
            .ThenBy(static settlement => settlement.DistrictId)
            .ThenBy(static settlement => settlement.QuarterId)
            .ThenBy(static settlement => settlement.Id)
            .ToArray();

        await WriteAsync(outputDirectory, "provinces", version, provenance, provinces).ConfigureAwait(false);
        await WriteAsync(outputDirectory, "districts", version, provenance, districts).ConfigureAwait(false);
        await WriteAsync(outputDirectory, "quarters", version, provenance, quarters).ConfigureAwait(false);
        await WriteAsync(outputDirectory, "neighborhoods", version, provenance, settlements).ConfigureAwait(false);

        var villages = settlements.Count(static settlement => settlement.Kind == SettlementKind.Village);

        Console.WriteLine(
            $"wrote    {provinces.Length} provinces, {districts.Length} districts, "
            + $"{quarters.Length} quarters, {settlements.Length} settlements "
            + $"({villages} villages, {settlements.Length - villages} neighbourhoods)");

        return 0;
    }

    /// <summary>
    /// Landline area codes (alan kodu) by plate code. Not part of the source export — it has
    /// no telephony fields at all — so this is the BTK numbering plan compiled directly into
    /// the tool, the same way <see cref="Province.PlateCode"/> itself is a stable government
    /// assignment rather than something derived. İstanbul ("34") is the only entry with two.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> AreaCodesByPlate =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["01"] = ["322"], ["02"] = ["416"], ["03"] = ["272"], ["04"] = ["472"], ["05"] = ["358"],
            ["06"] = ["312"], ["07"] = ["242"], ["08"] = ["466"], ["09"] = ["256"], ["10"] = ["266"],
            ["11"] = ["228"], ["12"] = ["426"], ["13"] = ["434"], ["14"] = ["374"], ["15"] = ["248"],
            ["16"] = ["224"], ["17"] = ["286"], ["18"] = ["376"], ["19"] = ["364"], ["20"] = ["258"],
            ["21"] = ["412"], ["22"] = ["284"], ["23"] = ["424"], ["24"] = ["446"], ["25"] = ["442"],
            ["26"] = ["222"], ["27"] = ["342"], ["28"] = ["454"], ["29"] = ["456"], ["30"] = ["438"],
            ["31"] = ["326"], ["32"] = ["246"], ["33"] = ["324"], ["34"] = ["212", "216"], ["35"] = ["232"],
            ["36"] = ["474"], ["37"] = ["366"], ["38"] = ["352"], ["39"] = ["288"], ["40"] = ["386"],
            ["41"] = ["262"], ["42"] = ["332"], ["43"] = ["274"], ["44"] = ["422"], ["45"] = ["236"],
            ["46"] = ["344"], ["47"] = ["482"], ["48"] = ["252"], ["49"] = ["436"], ["50"] = ["384"],
            ["51"] = ["388"], ["52"] = ["452"], ["53"] = ["464"], ["54"] = ["264"], ["55"] = ["362"],
            ["56"] = ["484"], ["57"] = ["368"], ["58"] = ["346"], ["59"] = ["282"], ["60"] = ["356"],
            ["61"] = ["462"], ["62"] = ["428"], ["63"] = ["414"], ["64"] = ["276"], ["65"] = ["432"],
            ["66"] = ["354"], ["67"] = ["372"], ["68"] = ["382"], ["69"] = ["458"], ["70"] = ["338"],
            ["71"] = ["318"], ["72"] = ["488"], ["73"] = ["486"], ["74"] = ["378"], ["75"] = ["478"],
            ["76"] = ["476"], ["77"] = ["226"], ["78"] = ["370"], ["79"] = ["348"], ["80"] = ["328"],
            ["81"] = ["380"],
        };

    private static Province MapProvince(JsonElement element, IReadOnlyDictionary<int, int> districtCounts)
    {
        var id = element.GetProperty("IlId").GetInt32();
        var name = Text(element, "IlAdi");
        var plateCode = Text(element, "Plaka");

        if (!AreaCodesByPlate.TryGetValue(plateCode, out var areaCodes))
        {
            throw new InvalidDataException($"No area code mapping for plate code '{plateCode}' ({name}).");
        }

        return new Province
        {
            Id = id,
            PlateCode = plateCode,
            Name = name,
            NameUpper = Text(element, "IlAdiBuyuk"),
            Slug = SearchTextNormalizer.Slugify(name),
            AreaCodes = areaCodes,
            DistrictCount = districtCounts.GetValueOrDefault(id),
        };
    }

    private static District MapDistrict(
        JsonElement element,
        IReadOnlyDictionary<int, string> provinceNames,
        IReadOnlyDictionary<int, int> quarterCounts)
    {
        var id = element.GetProperty("IlceId").GetInt32();
        var provinceId = element.GetProperty("IlId").GetInt32();
        var name = Text(element, "IlceAdi");

        return new District
        {
            Id = id,
            Name = name,
            NameUpper = Text(element, "IlceAdiBuyuk"),
            Slug = SearchTextNormalizer.Slugify(name),
            ProvinceId = provinceId,
            // Denormalized so that a district response is useful without a second request.
            ProvinceName = provinceNames.GetValueOrDefault(provinceId),
            QuarterCount = quarterCounts.GetValueOrDefault(id),
        };
    }

    private static Quarter MapQuarter(
        JsonElement element,
        IReadOnlyDictionary<int, string> provinceNames,
        IReadOnlyDictionary<int, string> districtNames,
        IReadOnlyDictionary<int, int> settlementCounts)
    {
        var id = element.GetProperty("SemtId").GetInt32();
        var provinceId = element.GetProperty("IlId").GetInt32();
        var districtId = element.GetProperty("IlceId").GetInt32();
        var name = Text(element, "SemtAdi");

        return new Quarter
        {
            Id = id,
            Name = name,
            NameUpper = Text(element, "SemtAdiBuyuk"),
            Slug = SearchTextNormalizer.Slugify(name),
            ProvinceId = provinceId,
            ProvinceName = provinceNames.GetValueOrDefault(provinceId),
            DistrictId = districtId,
            DistrictName = districtNames.GetValueOrDefault(districtId),
            PostalCode = Text(element, "PostaKodu"),
            SettlementCount = settlementCounts.GetValueOrDefault(id),
        };
    }

    private static Neighborhood MapSettlement(
        JsonElement element,
        IReadOnlyDictionary<int, string> postalCodes)
    {
        var quarterId = element.GetProperty("SemtId").GetInt32();
        var name = Text(element, "MahalleAdi");
        var upper = Text(element, "MahalleAdiBuyuk");

        if (!postalCodes.TryGetValue(quarterId, out var postalCode))
        {
            // The settlement would carry no postal code and belong to nothing. Fail loudly
            // rather than emit a row the API can serve but not place.
            throw new InvalidDataException(
                $"Settlement '{name}' references quarter {quarterId}, which is not in the quarter file.");
        }

        var (kind, villageName) = Classify(name, upper);

        return new Neighborhood
        {
            Id = element.GetProperty("MahId").GetInt32(),
            Name = name,
            Slug = SearchTextNormalizer.Slugify(name),
            Kind = kind,
            ProvinceId = element.GetProperty("IlId").GetInt32(),
            DistrictId = element.GetProperty("IlceId").GetInt32(),
            QuarterId = quarterId,
            PostalCode = postalCode,
            VillageName = villageName,
        };
    }

    /// <summary>
    /// Splits a settlement name into its kind and, for a rural neighbourhood, the village it
    /// sits in.
    /// </summary>
    /// <remarks>
    /// The source encodes the distinction in the name itself, in three shapes: "X KÖYÜ" is a
    /// village, "X MAH" an urban neighbourhood, and "X MAH (Y KÖYÜ)" a neighbourhood inside
    /// village Y. The mahalle marker is therefore tested first — a name carrying both markers
    /// is the third shape, and reading it as a village would file 31,391 neighbourhoods under
    /// the wrong kind.
    /// </remarks>
    private static (SettlementKind Kind, string? VillageName) Classify(string name, string upper)
    {
        if (!IsMahalle(upper))
        {
            return (SettlementKind.Village, null);
        }

        var open = name.LastIndexOf('(');
        var close = name.LastIndexOf(')');

        if (open >= 0 && close > open + 1)
        {
            var inner = name[(open + 1)..close].Trim();

            if (inner.Length > 0)
            {
                return (SettlementKind.Neighborhood, inner);
            }
        }

        return (SettlementKind.Neighborhood, null);
    }

    /// <summary>
    /// True when the name carries the mahalle marker as a whole word, so that a village whose
    /// name merely starts with those letters ("MAHMUTLAR KÖYÜ") is not misread as one.
    /// </summary>
    private static bool IsMahalle(string upper)
    {
        var index = upper.IndexOf("MAH", StringComparison.Ordinal);

        while (index >= 0)
        {
            var startsWord = index == 0 || !char.IsLetter(upper[index - 1]);
            var after = index + 3;
            var endsWord = after >= upper.Length || !char.IsLetter(upper[after]);

            // "MAH" alone, or the spelled-out "MAHALLE"/"MAHALLESİ".
            if (startsWord && (endsWord || upper.AsSpan(index).StartsWith("MAHALLE", StringComparison.Ordinal)))
            {
                return true;
            }

            index = upper.IndexOf("MAH", index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static Task WriteAsync<T>(
        string directory,
        string name,
        string version,
        string source,
        IReadOnlyList<T> items) =>
        DataFileWriter.WriteAsync(
            Path.Combine(directory, $"{name}.json"),
            new DataSet<T>
            {
                Name = name,
                Version = version,
                Source = source,
                License = "MIT",
                Items = items,
            });

    /// <summary>Counts rows by an integer foreign key, for the "how many below me" fields.</summary>
    private static Dictionary<int, int> Tally(IReadOnlyList<JsonElement> rows, string property)
    {
        var counts = new Dictionary<int, int>();

        foreach (var row in rows)
        {
            var key = row.GetProperty(property).GetInt32();
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    /// <summary>Reads the single array a source file wraps its rows in.</summary>
    private static IReadOnlyList<JsonElement> Rows(JsonDocument document, string property)
    {
        if (!document.RootElement.TryGetProperty(property, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Source file has no '{property}' array.");
        }

        return [.. array.EnumerateArray()];
    }

    private static async Task<JsonDocument> ReadAsync(string directory, string name)
    {
        var path = Path.Combine(directory, $"{name}.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Source file '{path}' not found. The Turkish address export is not committed "
                + "to this repository — pass --source with the directory holding your copy "
                + "(iller, ilceler, semtler, mahalle-ve-koyler). Regenerating is only needed "
                + "to change how the data is derived; the committed data/*.json files are what "
                + "the API serves.",
                path);
        }

        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.GetString() is { Length: > 0 } text
            ? text.Trim()
            : throw new InvalidDataException($"Source row is missing '{property}'.");
}
