namespace YaMiSoFt.OpenData.Core.Validation;

/// <summary>
/// T.C. Kimlik No (Turkish national identity number) checksum validation (BRD §3.1 Faz 3).
/// </summary>
/// <remarks>
/// Format and checksum only. This confirms the eleven digits are internally consistent under
/// the published algorithm — it does not confirm the number is assigned to a real person, and
/// nothing here looks one up. That distinction is the whole reason this endpoint is in scope
/// at all: BRD §3.2 excludes any dataset that could identify an individual (KVKK), and a
/// checksum is the one thing about this number that carries no personal data.
/// </remarks>
public static class TcKimlikValidator
{
    private const int Length = 11;

    /// <summary>
    /// Validates <paramref name="value"/>. <paramref name="normalized"/> is the input with
    /// surrounding whitespace removed — what was actually checked, safe to echo back.
    /// </summary>
    public static bool Validate(string? value, out string normalized, out string? reason)
    {
        normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length != Length || !IsAllAsciiDigits(normalized))
        {
            reason = "invalid-format";
            return false;
        }

        // The leading digit is never zero — a real allocation rule, not a checksum fact, but a
        // malformed number this cheap to reject before doing the arithmetic below.
        if (normalized[0] == '0')
        {
            reason = "invalid-format";
            return false;
        }

        Span<int> digits = stackalloc int[Length];
        for (var i = 0; i < Length; i++)
        {
            digits[i] = normalized[i] - '0';
        }

        var oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
        var evenSum = digits[1] + digits[3] + digits[5] + digits[7];
        var tenthDigit = ((oddSum * 7) - evenSum) % 10;

        if (tenthDigit < 0)
        {
            tenthDigit += 10;
        }

        if (tenthDigit != digits[9])
        {
            reason = "invalid-checksum";
            return false;
        }

        var sumOfFirstTen = 0;
        for (var i = 0; i < 10; i++)
        {
            sumOfFirstTen += digits[i];
        }

        if (sumOfFirstTen % 10 != digits[10])
        {
            reason = "invalid-checksum";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool IsAllAsciiDigits(string value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
