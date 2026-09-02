namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>What kind of holiday an entry is.</summary>
public enum HolidayKind
{
    /// <summary>A national holiday fixed to a Gregorian date.</summary>
    National = 0,

    /// <summary>A religious holiday whose date follows the Hijri calendar.</summary>
    Religious = 1,
}

/// <summary>
/// One official non-working day in Turkey (BRD 3.1, Faz 2).
/// </summary>
/// <remarks>
/// Half days are separate entries rather than a flag on the following day, because that is
/// how they work in practice: the afternoon before a religious holiday, and the afternoon of
/// 28 October, are non-working while the mornings are not. A caller computing working days
/// needs them to be addressable on their own date.
/// </remarks>
public sealed record Holiday
{
    /// <summary>The calendar date, in the Gregorian calendar.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Turkish name, e.g. "Cumhuriyet Bayrami".</summary>
    public required string NameTr { get; init; }

    /// <summary>English name, e.g. "Republic Day".</summary>
    public required string NameEn { get; init; }

    /// <summary>Whether the date is fixed or follows the Hijri calendar.</summary>
    public required HolidayKind Kind { get; init; }

    /// <summary>
    /// True when only part of the day is a holiday: the afternoon of a religious holiday's
    /// eve, and the afternoon of 28 October.
    /// </summary>
    public required bool IsHalfDay { get; init; }

    /// <summary>Calendar year the date falls in.</summary>
    public int Year => Date.Year;
}
