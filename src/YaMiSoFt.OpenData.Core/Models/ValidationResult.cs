namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// Result of a stateless format/checksum check (BRD §3.1 Faz 3: IBAN, T.C. Kimlik No).
/// </summary>
/// <remarks>
/// Unlike every other response in this API, nothing here is backed by a committed dataset —
/// there is no version to stamp an ETag with, so these endpoints skip <c>DataResult</c>
/// entirely and return this directly.
/// </remarks>
public sealed record ValidationResult
{
    /// <summary>The input as it was actually checked (whitespace trimmed, IBAN upper-cased).</summary>
    public required string Value { get; init; }

    /// <summary>True when the value passes both the format and checksum checks.</summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Machine-readable reason the value failed — "invalid-length", "invalid-format" or
    /// "invalid-checksum" — null when <see cref="IsValid"/> is true.
    /// </summary>
    public string? Reason { get; init; }
}
