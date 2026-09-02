using System.Text.Json;
using System.Text.Json.Serialization;

namespace YaMiSoFt.OpenData.DataTool;

/// <summary>
/// Writes a dataset file in the shape the API expects.
/// </summary>
/// <remarks>
/// Every command writes through here so the committed files stay byte-consistent with one
/// another: same indentation, same casing, same trailing newline. That matters because these
/// files are reviewed as diffs — a formatting difference between two generators would show up
/// as spurious churn in a data PR.
/// </remarks>
public static class DataFileWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Matches the API's own encoding so a committed file reads the way a response does.
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes <paramref name="dataset"/> to <paramref name="path"/>.</summary>
    public static async Task WriteAsync<T>(string path, T dataset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var json = JsonSerializer.Serialize(dataset, Options);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, json + "\n").ConfigureAwait(false);
        Console.WriteLine($"wrote    {path}");
    }
}
