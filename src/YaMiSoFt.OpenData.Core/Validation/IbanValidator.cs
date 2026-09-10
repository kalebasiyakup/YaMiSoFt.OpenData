using System.Globalization;
using System.Numerics;
using System.Text;

namespace YaMiSoFt.OpenData.Core.Validation;

/// <summary>
/// IBAN format and checksum validation (BRD §3.1 Faz 3, ISO 13616 / ISO 7064 MOD-97-10).
/// </summary>
/// <remarks>
/// Calculation only, on purpose — no bank or country database is consulted. The one piece of
/// country-specific knowledge baked in is Turkey's fixed length (26), because that is this
/// project's own primary audience and a wrong length is the single most common IBAN typo;
/// every other country is checked structurally and by checksum, not against a length table
/// this project would then have to keep current for ~70 jurisdictions.
/// </remarks>
public static class IbanValidator
{
    // ISO 13616 bounds: Norway (NO) is the shortest real-world IBAN at 15, the format allows
    // up to 34.
    private const int MinLength = 15;
    private const int MaxLength = 34;
    private const int TurkeyLength = 26;

    /// <summary>
    /// Validates <paramref name="iban"/>. <paramref name="normalized"/> is the input with
    /// spaces removed and letters upper-cased — what was actually checked, and safe to echo
    /// back to the caller.
    /// </summary>
    public static bool Validate(string? iban, out string normalized, out string? reason)
    {
        normalized = Normalize(iban);

        if (normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            reason = "invalid-length";
            return false;
        }

        if (!IsWellFormed(normalized))
        {
            reason = "invalid-format";
            return false;
        }

        if (normalized.StartsWith("TR", StringComparison.Ordinal) && normalized.Length != TurkeyLength)
        {
            reason = "invalid-length";
            return false;
        }

        if (!HasValidChecksum(normalized))
        {
            reason = "invalid-checksum";
            return false;
        }

        reason = null;
        return true;
    }

    private static string Normalize(string? iban) =>
        string.IsNullOrWhiteSpace(iban)
            ? string.Empty
            : iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static bool IsWellFormed(string iban)
    {
        if (!char.IsAsciiLetterUpper(iban[0]) || !char.IsAsciiLetterUpper(iban[1]) ||
            !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3]))
        {
            return false;
        }

        for (var i = 4; i < iban.Length; i++)
        {
            if (!char.IsAsciiLetterUpper(iban[i]) && !char.IsAsciiDigit(iban[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// ISO 7064 MOD-97-10: move the first four characters to the end, replace each letter with
    /// its alphabet position + 9 (A=10 ... Z=35), and the resulting decimal number must be
    /// congruent to 1 mod 97. <see cref="BigInteger"/> is used because a full-length IBAN
    /// expands to well over the ~19 digits a <see cref="long"/> can hold.
    /// </summary>
    private static bool HasValidChecksum(string iban)
    {
        var rearranged = string.Concat(iban.AsSpan(4), iban.AsSpan(0, 4));
        var numeric = new StringBuilder(rearranged.Length * 2);

        foreach (var c in rearranged)
        {
            if (char.IsAsciiDigit(c))
            {
                numeric.Append(c);
            }
            else
            {
                numeric.Append((c - 'A' + 10).ToString(CultureInfo.InvariantCulture));
            }
        }

        return BigInteger.Parse(numeric.ToString(), CultureInfo.InvariantCulture) % 97 == BigInteger.One;
    }
}
