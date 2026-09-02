namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>Whether a settlement is a neighbourhood or a village.</summary>
public enum SettlementKind
{
    /// <summary>Mahalle: a named neighbourhood, urban or inside a village.</summary>
    Neighborhood = 0,

    /// <summary>Köy: a village recorded as a settlement in its own right.</summary>
    Village = 1,
}

/// <summary>
/// A Turkish neighbourhood or village, the finest level of the address hierarchy
/// (BRD 3.1, Faz 2).
/// </summary>
/// <remarks>
/// Neighbourhoods and villages share one record type because they share one role — the last
/// component of an address — and callers filling an address form need them in a single list.
/// The distinction is kept in <see cref="Kind"/> for the callers that care.
///
/// Unlike the levels above it, a settlement carries no upper-case name. The other levels are
/// small enough that the redundancy is free; at 73,552 rows it costs about 2.5 MB of a file
/// that is parsed on a cold start, to publish a value a caller can produce from
/// <see cref="Name"/> with a tr-TR culture.
///
/// The postal code is denormalized from the owning quarter (see <see cref="Quarter"/>), where
/// it is actually assigned. It is repeated here so a settlement response is a complete address
/// line without a second request, which is the same reason a district carries its province
/// name.
/// </remarks>
public sealed record Neighborhood
{
    /// <summary>Stable settlement identifier from the source dataset.</summary>
    public required int Id { get; init; }

    /// <summary>Name in Turkish, e.g. "Abbasağa Mah".</summary>
    public required string Name { get; init; }

    /// <summary>URL-safe form of the name.</summary>
    public required string Slug { get; init; }

    /// <summary>Whether this is a neighbourhood or a village.</summary>
    public required SettlementKind Kind { get; init; }

    /// <summary>Plate code of the province.</summary>
    public required int ProvinceId { get; init; }

    /// <summary>Identifier of the district.</summary>
    public required int DistrictId { get; init; }

    /// <summary>Identifier of the quarter (semt) the settlement belongs to.</summary>
    public required int QuarterId { get; init; }

    /// <summary>Five-digit postal code, denormalized from the quarter.</summary>
    public required string PostalCode { get; init; }

    /// <summary>
    /// The village a rural neighbourhood belongs to, where the source names one — 31,391
    /// records read "X Mah (Y Köyü)", meaning a neighbourhood inside village Y. Null for
    /// urban neighbourhoods and for villages themselves.
    /// </summary>
    public string? VillageName { get; init; }
}
