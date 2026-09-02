namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// A Turkish district (ilçe), the second level of the address hierarchy. Immutable; loaded
/// once at startup.
/// </summary>
public sealed record District
{
    /// <summary>Stable district identifier from the source dataset.</summary>
    public required int Id { get; init; }

    /// <summary>District name in Turkish, e.g. "Beşiktaş".</summary>
    public required string Name { get; init; }

    /// <summary>Name in Turkish upper case, as the source dataset publishes it.</summary>
    public required string NameUpper { get; init; }

    /// <summary>URL-safe form of the name.</summary>
    public required string Slug { get; init; }

    /// <summary>Plate code of the province this district belongs to.</summary>
    public required int ProvinceId { get; init; }

    /// <summary>Province name, denormalized so a district reads standalone.</summary>
    public string? ProvinceName { get; init; }

    /// <summary>Number of quarters (semt) in the district.</summary>
    public int QuarterCount { get; init; }
}
