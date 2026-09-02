using System.Collections.Frozen;
using System.Collections.Immutable;
using YaMiSoFt.OpenData.Core.Models;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// In-memory lookup of Turkey's official non-working days, grouped by year (BRD 3.1, Faz 2).
/// </summary>
/// <remarks>
/// Keyed by year rather than by a code, because that is the only question anyone asks of a
/// holiday calendar. The covered range is exposed so the API can tell a caller asking about
/// 2099 that the year is outside the dataset, rather than returning an empty list that reads
/// as "no holidays that year".
/// </remarks>
public sealed class HolidayStore
{
    private readonly FrozenDictionary<int, ImmutableArray<Holiday>> _byYear;

    /// <summary>Builds the store from a loaded dataset.</summary>
    /// <exception cref="InvalidDataException">The dataset is empty.</exception>
    public HolidayStore(DataSet<Holiday> dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        Version = dataset.Version;
        Source = dataset.Source;
        License = dataset.License;
        All = [.. dataset.Items.OrderBy(static holiday => holiday.Date)];

        if (All.Count == 0)
        {
            throw new InvalidDataException("The holidays dataset is empty.");
        }

        _byYear = All
            .GroupBy(static holiday => holiday.Year)
            .ToFrozenDictionary(
                static group => group.Key,
                static group => group.OrderBy(static holiday => holiday.Date).ToImmutableArray());

        FirstYear = _byYear.Keys.Min();
        LastYear = _byYear.Keys.Max();
    }

    /// <summary>Dataset version stamp.</summary>
    public string Version { get; }

    /// <summary>How the dataset was produced.</summary>
    public string Source { get; }

    /// <summary>SPDX licence identifier of the dataset.</summary>
    public string License { get; }

    /// <summary>Every holiday, date ascending.</summary>
    public IReadOnlyList<Holiday> All { get; }

    /// <summary>Number of holiday entries held.</summary>
    public int Count => All.Count;

    /// <summary>Earliest year covered.</summary>
    public int FirstYear { get; }

    /// <summary>Latest year covered.</summary>
    public int LastYear { get; }

    /// <summary>True when <paramref name="year"/> falls inside the covered range.</summary>
    public bool Covers(int year) => year >= FirstYear && year <= LastYear;

    /// <summary>Returns the holidays of one year, date ascending. Empty if not covered.</summary>
    public IReadOnlyList<Holiday> ForYear(int year) =>
        _byYear.TryGetValue(year, out var holidays) ? holidays : [];
}
