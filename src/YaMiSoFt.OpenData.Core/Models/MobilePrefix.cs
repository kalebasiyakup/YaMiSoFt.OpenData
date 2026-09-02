namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// A valid Turkish mobile number prefix (BRD 3.1, Faz 2).
/// </summary>
/// <remarks>
/// Deliberately carries no operator name. Turkey has had mobile number portability since
/// 2008, so a prefix records which operator the range was originally allocated to and says
/// nothing about who carries a given number today — libphonenumber flags Turkey as a
/// portable region for exactly this reason. Publishing an operator field would be data that
/// looks authoritative and is wrong for millions of numbers.
///
/// What the prefix is genuinely good for is validation: whether a number could be a Turkish
/// mobile number at all.
/// </remarks>
public sealed record MobilePrefix
{
    /// <summary>Three-digit prefix in national format without the trunk zero, e.g. "532".</summary>
    public required string Prefix { get; init; }

    /// <summary>The same prefix in international format, e.g. "90532".</summary>
    public required string International { get; init; }
}
