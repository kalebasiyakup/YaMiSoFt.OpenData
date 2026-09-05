namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// A Turkish province (il), the top level of the address hierarchy. Immutable; loaded once
/// at startup.
/// </summary>
/// <remarks>
/// The record carries the administrative facts the source dataset publishes and nothing else.
/// Population, area and coordinates were dropped when the hierarchy moved to the curated
/// address dataset: keeping them would have meant serving figures from a different vintage
/// than the hierarchy they are attached to, with no way for a caller to tell which is which.
/// </remarks>
public sealed record Province
{
    /// <summary>
    /// Licence plate code, 1-81. This doubles as the identifier because it is the number
    /// Turkish users already know a province by, and it is stable.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>Zero-padded plate code, e.g. "01". The form that appears on a number plate.</summary>
    public required string PlateCode { get; init; }

    /// <summary>Province name in Turkish, e.g. "Kahramanmaraş".</summary>
    public required string Name { get; init; }

    /// <summary>Name in Turkish upper case, as the source dataset publishes it.</summary>
    public required string NameUpper { get; init; }

    /// <summary>URL-safe form of the name, e.g. "kahramanmaras".</summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Landline area code(s) (alan kodu), three digits, no trunk "0". Every province has
    /// exactly one except İstanbul, which has two — "212" (Avrupa Yakası) and "216" (Anadolu
    /// Yakası) — the only province the 2000 numbering plan split this way.
    /// </summary>
    public required IReadOnlyList<string> AreaCodes { get; init; }

    /// <summary>Number of districts in the province.</summary>
    public int DistrictCount { get; init; }
}
