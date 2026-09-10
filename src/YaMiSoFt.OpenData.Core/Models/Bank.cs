namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>The regulatory category an institution operates under.</summary>
/// <remarks>
/// These are the BDDK licence categories, not a classification invented here. The TCMB
/// participant list the dataset is compiled from does not carry them, so they are added per
/// row — see DATA-LICENSES.md for the rule and the handful of rows it does not decide.
/// </remarks>
public enum BankType
{
    /// <summary>Mevduat bankası: takes deposits. The default category, digital-only included.</summary>
    Deposit = 0,

    /// <summary>Katılım bankası: operates on participation (interest-free) principles.</summary>
    Participation = 1,

    /// <summary>Kalkınma ve yatırım bankası: neither takes deposits nor participation funds.</summary>
    DevelopmentInvestment = 2,

    /// <summary>Türkiye Cumhuriyet Merkez Bankası. Exactly one row.</summary>
    CentralBank = 3,

    /// <summary>
    /// A payment-system participant that is not a bank at all — the TCMB list carries two
    /// (MKK and PTT) and dropping them would leave a hole in the code-to-institution map.
    /// </summary>
    Other = 4,
}

/// <summary>
/// A participant in the TCMB payment systems, keyed by its EFT code (BRD 3.1, Faz 3).
/// </summary>
/// <remarks>
/// The EFT code is what makes this dataset worth publishing: it is the bank identifier
/// embedded in every Turkish IBAN, so a caller holding an IBAN can name the institution
/// without asking anyone. See <c>BankStore.FindByIban</c> for the extraction.
///
/// <see cref="LegalName"/> is TCMB's own string, carried verbatim so a row can be checked
/// against the source list character for character. <see cref="Name"/> is a curated display
/// name for the same institution ("Garanti BBVA" for "T. GARANTİ BANKASI A.Ş.") and is the
/// one field here that is this project's editorial choice rather than a transcription —
/// callers that need the authoritative string want <see cref="LegalName"/>.
///
/// No BIC/SWIFT field, deliberately: see DATA-LICENSES.md. Adding one later is additive;
/// publishing a wrong one is not something a caller can detect.
/// </remarks>
public sealed record Bank
{
    /// <summary>
    /// Four-digit TCMB EFT code, zero-padded, e.g. "0046". Turkish IBANs carry this same
    /// value zero-padded to five digits ("00046").
    /// </summary>
    public required string Code { get; init; }

    /// <summary>Curated display name, e.g. "Akbank".</summary>
    public required string Name { get; init; }

    /// <summary>TCMB's registered name, verbatim, e.g. "AKBANK T.A.Ş.".</summary>
    public required string LegalName { get; init; }

    /// <summary>Regulatory category.</summary>
    public required BankType Type { get; init; }
}
