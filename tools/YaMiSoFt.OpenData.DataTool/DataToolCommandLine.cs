namespace YaMiSoFt.OpenData.DataTool;

/// <summary>Argument parsing and dispatch for the data regeneration tool.</summary>
public static class DataToolCommandLine
{
    private const string Usage = """
        OpenData data regeneration tool.

        Usage:
          datatool turkey      --source <dir>      --output <dir>
          datatool currencies                      --output <dir>
          datatool languages                       --output <dir>
          datatool holidays    --from <year> --to <year> --output <dir>
          datatool all                             --output <dir>

        Options:
          --source   The Turkish address export directory for 'turkey' (default data/source).
          --output   Destination file, or directory for the multi-file commands.

        Output is meant to be committed. Review the diff before opening a PR.
        """;

    /// <summary>Runs the requested command. Returns a process exit code.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        var options = ParseOptions(args.AsSpan(1));

        try
        {
            return args[0] switch
            {
                "turkey" => await TurkeyCommand.RunAsync(options).ConfigureAwait(false),
                "currencies" => await IcuCommand.RunCurrenciesAsync(options).ConfigureAwait(false),
                "languages" => await IcuCommand.RunLanguagesAsync(options).ConfigureAwait(false),
                "holidays" => await HolidaysCommand.RunAsync(options).ConfigureAwait(false),
                "all" => await RunAllAsync(options).ConfigureAwait(false),
                var unknown => Fail($"Unknown command '{unknown}'."),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException
                                      or IOException
                                      or InvalidDataException
                                      or UnauthorizedAccessException)
        {
            return Fail(ex.Message);
        }
    }

    /// <summary>Regenerates every dataset in one pass.</summary>
    private static async Task<int> RunAllAsync(IReadOnlyDictionary<string, string> options)
    {
        var directory = options.GetValueOrDefault("output", "data");
        var perCommand = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["output"] = directory,
        };

        var results = new[]
        {
            await TurkeyCommand.RunAsync(perCommand).ConfigureAwait(false),
            await IcuCommand.RunCurrenciesAsync(perCommand).ConfigureAwait(false),
            await IcuCommand.RunLanguagesAsync(perCommand).ConfigureAwait(false),
            await HolidaysCommand.RunAsync(perCommand).ConfigureAwait(false),
        };

        return results.Any(static code => code != 0) ? 1 : 0;
    }

    private static Dictionary<string, string> ParseOptions(ReadOnlySpan<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal))
            {
                options[args[index][2..]] = args[index + 1];
            }
        }

        return options;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine();
        Console.Error.WriteLine(Usage);
        return 1;
    }
}
