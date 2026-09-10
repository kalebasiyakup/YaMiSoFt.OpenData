using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Core.Models;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// Reads committed dataset files from disk into memory (BRD 7.2, "veri = kod").
/// </summary>
public static class DataSetLoader
{
    /// <summary>Default dataset directory, relative to the application base directory.</summary>
    public const string DefaultDirectory = "data";

    /// <summary>Loads <c>provinces.json</c>.</summary>
    public static DataSet<Province> LoadProvinces(string directory) =>
        Load(directory, "provinces", OpenDataJson.TypeInfo<DataSet<Province>>());

    /// <summary>Loads <c>districts.json</c>.</summary>
    public static DataSet<District> LoadDistricts(string directory) =>
        Load(directory, "districts", OpenDataJson.TypeInfo<DataSet<District>>());

    /// <summary>Loads <c>quarters.json</c>.</summary>
    public static DataSet<Quarter> LoadQuarters(string directory) =>
        Load(directory, "quarters", OpenDataJson.TypeInfo<DataSet<Quarter>>());

    /// <summary>Loads <c>currencies.json</c>.</summary>
    public static DataSet<Currency> LoadCurrencies(string directory) =>
        Load(directory, "currencies", OpenDataJson.TypeInfo<DataSet<Currency>>());

    /// <summary>Loads <c>languages.json</c>.</summary>
    public static DataSet<LanguageInfo> LoadLanguages(string directory) =>
        Load(directory, "languages", OpenDataJson.TypeInfo<DataSet<LanguageInfo>>());

    /// <summary>Loads <c>countries.json</c>.</summary>
    public static DataSet<Country> LoadCountries(string directory) =>
        Load(directory, "countries", OpenDataJson.TypeInfo<DataSet<Country>>());

    /// <summary>Loads <c>holidays.json</c>.</summary>
    public static DataSet<Holiday> LoadHolidays(string directory) =>
        Load(directory, "holidays", OpenDataJson.TypeInfo<DataSet<Holiday>>());

    /// <summary>Loads <c>mobile-prefixes.json</c>.</summary>
    public static DataSet<MobilePrefix> LoadMobilePrefixes(string directory) =>
        Load(directory, "mobile-prefixes", OpenDataJson.TypeInfo<DataSet<MobilePrefix>>());

    /// <summary>Loads <c>mobile-operators.json</c>.</summary>
    public static DataSet<MobileOperator> LoadMobileOperators(string directory) =>
        Load(directory, "mobile-operators", OpenDataJson.TypeInfo<DataSet<MobileOperator>>());

    /// <summary>Loads <c>banks.json</c>.</summary>
    public static DataSet<Bank> LoadBanks(string directory) =>
        Load(directory, "banks", OpenDataJson.TypeInfo<DataSet<Bank>>());

    /// <summary>Loads <c>neighborhoods.json</c>.</summary>
    public static DataSet<Neighborhood> LoadNeighborhoods(string directory) =>
        Load(directory, "neighborhoods", OpenDataJson.TypeInfo<DataSet<Neighborhood>>());

    /// <summary>
    /// Reads <c>{name}.json</c> from <paramref name="directory"/>.
    /// </summary>
    /// <exception cref="FileNotFoundException">The dataset file is missing.</exception>
    /// <exception cref="InvalidDataException">The file is present but unreadable as a dataset.</exception>
    public static DataSet<T> Load<T>(string directory, string name, JsonTypeInfo<DataSet<T>> typeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(typeInfo);

        var path = Path.Combine(directory, $"{name}.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Dataset '{name}' not found at '{path}'. Run the data tool to regenerate it.",
                path);
        }

        using var stream = File.OpenRead(path);

        DataSet<T>? dataset;
        try
        {
            dataset = JsonSerializer.Deserialize(stream, typeInfo);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Dataset at '{path}' is not valid JSON.", ex);
        }

        return dataset ?? throw new InvalidDataException($"Dataset at '{path}' is empty.");
    }

    /// <summary>
    /// Resolves the dataset directory: <paramref name="configured"/> when set, otherwise
    /// <see cref="DefaultDirectory"/> under <paramref name="basePath"/>.
    /// </summary>
    public static string ResolveDirectory(string? configured, string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(basePath, DefaultDirectory);
        }

        return Path.IsPathRooted(configured) ? configured : Path.Combine(basePath, configured);
    }
}
