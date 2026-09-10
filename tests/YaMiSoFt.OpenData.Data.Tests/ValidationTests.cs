using YaMiSoFt.OpenData.Core.Validation;

namespace YaMiSoFt.OpenData.Data.Tests;

/// <summary>Covers IBAN and T.C. Kimlik No format/checksum validation (BRD §3.1 Faz 3).</summary>
public sealed class ValidationTests
{
    [Theory]
    // The BRD's own example, plus the IBANs most commonly cited as canonical valid examples
    // (Wikipedia's IBAN article, among others) — chosen so a broken checksum implementation
    // fails against numbers nobody could suspect are wrong.
    [InlineData("TR330006100519786457841326")]
    [InlineData("DE89370400440532013000")]
    [InlineData("GB29NWBK60161331926819")]
    // Lower case and spaced input must still validate — that is what "normalized" means.
    [InlineData("tr33 0006 1005 1978 6457 8413 26")]
    public void Valid_ibans_pass(string iban)
    {
        var isValid = IbanValidator.Validate(iban, out var normalized, out var reason);

        Assert.True(isValid, $"Expected valid, got reason '{reason}' for '{iban}'.");
        Assert.Null(reason);
        Assert.Equal(iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant(), normalized);
    }

    [Theory]
    // Last digit of a known-good IBAN flipped: checksum must fail, not merely "look plausible".
    [InlineData("TR330006100519786457841327", "invalid-checksum")]
    [InlineData("DE89370400440532013001", "invalid-checksum")]
    // Right prefix, wrong length for a Turkish IBAN (25 chars, one short of 26).
    [InlineData("TR3300061005197864578413", "invalid-length")]
    // Not remotely IBAN-shaped — too short to even reach the format check.
    [InlineData("hello world", "invalid-length")]
    [InlineData("", "invalid-length")]
    // Long enough to reach the format check, but not IBAN-shaped.
    [InlineData("hello world this is not an iban", "invalid-format")]
    [InlineData("TR", "invalid-length")]
    // Digits where the BBAN allows letters too, but the country/check-digit positions must be
    // letters then digits specifically.
    [InlineData("12330006100519786457841326", "invalid-format")]
    public void Invalid_ibans_are_rejected_with_the_right_reason(string iban, string expectedReason)
    {
        var isValid = IbanValidator.Validate(iban, out _, out var reason);

        Assert.False(isValid);
        Assert.Equal(expectedReason, reason);
    }

    [Theory]
    // Well-formed but algorithmically-generated valid T.C. Kimlik numbers (not real people —
    // the point of a checksum-only endpoint is that "real" isn't a thing it can claim).
    [InlineData("10000000146")]
    [InlineData("35467128950")]
    public void Valid_tc_kimlik_numbers_pass(string no)
    {
        var isValid = TcKimlikValidator.Validate(no, out var normalized, out var reason);

        Assert.True(isValid, $"Expected valid, got reason '{reason}' for '{no}'.");
        Assert.Null(reason);
        Assert.Equal(no, normalized);
    }

    [Theory]
    [InlineData("10000000147", "invalid-checksum")] // last digit of a valid number flipped
    [InlineData("00000000146", "invalid-format")]   // leading zero is never allocated
    [InlineData("1234567890", "invalid-format")]    // ten digits, not eleven
    [InlineData("123456789012", "invalid-format")]  // twelve digits
    [InlineData("1234567890a", "invalid-format")]   // not all digits
    [InlineData("", "invalid-format")]
    public void Invalid_tc_kimlik_numbers_are_rejected_with_the_right_reason(string no, string expectedReason)
    {
        var isValid = TcKimlikValidator.Validate(no, out _, out var reason);

        Assert.False(isValid);
        Assert.Equal(expectedReason, reason);
    }
}
