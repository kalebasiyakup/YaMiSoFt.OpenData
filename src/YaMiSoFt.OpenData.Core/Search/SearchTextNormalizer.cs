using System.Globalization;
using System.Text;

namespace YaMiSoFt.OpenData.Core.Search;

/// <summary>
/// Folds text to a diacritic-insensitive, case-insensitive search key (FR-05).
/// </summary>
/// <remarks>
/// Unicode decomposition alone is not enough for Turkish. Most Turkish letters do decompose
/// into a base letter plus a combining mark (g-breve, s-cedilla, u-diaeresis, ...), so
/// stripping non-spacing marks handles them. The dotless i (U+0131) does not: it is an
/// independent letter with no decomposition, and the dotted capital I (U+0130) lowercases
/// to "i" plus a combining dot only under some cultures. Both are mapped explicitly so that
/// "Istanbul", "istanbul", "ISTANBUL" and "Istanbul" all collapse to the same key.
/// </remarks>
public static class SearchTextNormalizer
{
    /// <summary>
    /// Returns the search key for <paramref name="value"/>: lowercase, diacritic-free,
    /// whitespace-collapsed. Returns an empty string for null or blank input.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var rune in value.Trim())
        {
            var mapped = MapTurkish(rune);
            if (mapped != '\0')
            {
                builder.Append(mapped);
                continue;
            }

            builder.Append(rune);
        }

        var decomposed = builder.ToString().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var lastWasSpace = false;

        foreach (var rune in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(rune) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsWhiteSpace(rune))
            {
                // Collapse runs of whitespace so "Kahramanmaras" spacing variants still match.
                if (!lastWasSpace && result.Length > 0)
                {
                    result.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            result.Append(char.ToLowerInvariant(rune));
            lastWasSpace = false;
        }

        if (result.Length > 0 && result[^1] == ' ')
        {
            result.Length--;
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Returns the URL-safe slug for <paramref name="value"/>: the search key with every run
    /// of non-alphanumeric characters collapsed to a single hyphen.
    /// </summary>
    /// <remarks>
    /// Slugs are generated from this method and resolved through it, so a caller can pass the
    /// display name ("Kahramanmaraş", "Şehit Kubilay Mah.") and land on the stored slug. Two
    /// separate implementations would drift the first time a name contained punctuation the
    /// generator handled and the resolver did not.
    /// </remarks>
    public static string Slugify(string? value)
    {
        var normalized = Normalize(value);

        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var slug = new StringBuilder(normalized.Length);

        foreach (var rune in normalized)
        {
            if (char.IsAsciiLetterOrDigit(rune))
            {
                slug.Append(rune);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        while (slug.Length > 0 && slug[^1] == '-')
        {
            slug.Length--;
        }

        return slug.ToString();
    }

    /// <summary>
    /// Returns the ASCII replacement for a Turkish-specific letter, or '\0' when the caller
    /// should fall through to Unicode decomposition.
    /// </summary>
    private static char MapTurkish(char value) => value switch
    {
        '\u0130' => 'i', // I with dot above
        '\u0131' => 'i', // dotless i
        'I' => 'i',
        '\u011E' or '\u011F' => 'g', // G with breve
        '\u015E' or '\u015F' => 's', // S with cedilla
        '\u00C7' or '\u00E7' => 'c', // C with cedilla
        '\u00D6' or '\u00F6' => 'o', // O with diaeresis
        '\u00DC' or '\u00FC' => 'u', // U with diaeresis
        _ => '\0',
    };

    /// <summary>
    /// True when <paramref name="candidateKey"/> (already normalized) contains the
    /// normalized form of <paramref name="normalizedTerm"/>.
    /// </summary>
    public static bool Matches(string candidateKey, string normalizedTerm) =>
        normalizedTerm.Length == 0 ||
        candidateKey.Contains(normalizedTerm, StringComparison.Ordinal);
}
