namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// An ISO 4217 currency. Immutable; loaded once at startup.
/// </summary>
public sealed record Currency
{
    /// <summary>ISO 4217 alphabetic code, uppercase, e.g. "TRY".</summary>
    public required string Code { get; init; }

    /// <summary>English name, e.g. "Turkish Lira".</summary>
    public required string NameEn { get; init; }

    /// <summary>
    /// Turkish name. Falls back to <see cref="NameEn"/> where no Turkish name is available,
    /// so the field is never null; those rows are what a community PR should improve first.
    /// </summary>
    public required string NameTr { get; init; }

    /// <summary>Currency symbol, e.g. "TL".</summary>
    public string? Symbol { get; init; }

    /// <summary>
    /// Digits after the decimal separator: 2 for most currencies, 0 for the yen, 3 for the
    /// Kuwaiti dinar. Rounding money without this is a correctness bug, not a display detail.
    /// </summary>
    public required int DecimalDigits { get; init; }
}
