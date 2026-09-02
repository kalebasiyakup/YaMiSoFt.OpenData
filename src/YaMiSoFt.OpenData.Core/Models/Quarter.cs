namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// A Turkish quarter (semt), the third level of the address hierarchy: the delivery zone
/// between a district and the neighbourhoods and villages inside it.
/// </summary>
/// <remarks>
/// This level exists in the model because it is where the postal code actually lives. A
/// Turkish postal code is assigned per quarter, not per settlement — every neighbourhood or
/// village under a quarter shares the quarter's code. Modelling the quarter makes that a
/// stated one-to-many relationship rather than a value repeated 73,552 times with no owner,
/// and it gives postal code lookup an answer that costs 2,433 rows instead of loading the
/// whole settlement dataset.
/// </remarks>
public sealed record Quarter
{
    /// <summary>Stable quarter identifier from the source dataset.</summary>
    public required int Id { get; init; }

    /// <summary>Quarter name in Turkish, e.g. "Levent".</summary>
    public required string Name { get; init; }

    /// <summary>Name in Turkish upper case, as the source dataset publishes it.</summary>
    public required string NameUpper { get; init; }

    /// <summary>URL-safe form of the name.</summary>
    public required string Slug { get; init; }

    /// <summary>Plate code of the province.</summary>
    public required int ProvinceId { get; init; }

    /// <summary>Province name, denormalized so a quarter reads standalone.</summary>
    public string? ProvinceName { get; init; }

    /// <summary>Identifier of the district.</summary>
    public required int DistrictId { get; init; }

    /// <summary>District name, denormalized so a quarter reads standalone.</summary>
    public string? DistrictName { get; init; }

    /// <summary>
    /// Five-digit postal code. Unique across quarters, which is what makes
    /// <c>/postal-codes/{code}</c> resolve to exactly one quarter.
    /// </summary>
    public required string PostalCode { get; init; }

    /// <summary>Number of neighbourhoods and villages under the quarter.</summary>
    public int SettlementCount { get; init; }
}
