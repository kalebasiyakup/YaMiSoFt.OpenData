namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// One of Turkey's licensed mobile network operators. Immutable; loaded once at startup.
/// </summary>
/// <remarks>
/// Deliberately not linked to <see cref="MobilePrefix"/>. Number portability since 2008 means
/// a prefix's original allocation does not say who carries a given number today, and this
/// project does not publish a field that would look authoritative and be wrong for millions of
/// numbers — see <see cref="MobilePrefix"/>'s own remarks. This record carries only the
/// operators themselves: names that do not change with a customer's porting history.
/// </remarks>
public sealed record MobileOperator
{
    /// <summary>Slug identifier, e.g. "turkcell".</summary>
    public required string Code { get; init; }

    /// <summary>Brand name, e.g. "Turkcell".</summary>
    public required string Name { get; init; }

    /// <summary>Registered legal name, e.g. "Turkcell İletişim Hizmetleri A.Ş.".</summary>
    public required string LegalName { get; init; }
}
