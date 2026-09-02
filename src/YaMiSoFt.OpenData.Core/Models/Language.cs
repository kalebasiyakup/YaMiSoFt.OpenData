namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// An ISO 639 language. Immutable; loaded once at startup.
/// </summary>
/// <remarks>
/// Named <c>LanguageInfo</c> rather than <c>Language</c> because
/// <see cref="Querying.Language"/> already names the response-language selector, and having
/// two different meanings behind one identifier is worse than a slightly longer name.
/// </remarks>
public sealed record LanguageInfo
{
    /// <summary>ISO 639-1 two-letter code, lowercase, e.g. "tr".</summary>
    public required string Alpha2 { get; init; }

    /// <summary>ISO 639-2/T three-letter code, lowercase, e.g. "tur".</summary>
    public required string Alpha3 { get; init; }

    /// <summary>English name, e.g. "Turkish".</summary>
    public required string NameEn { get; init; }

    /// <summary>Turkish name, e.g. "Turkce".</summary>
    public required string NameTr { get; init; }

    /// <summary>Name in the language itself, e.g. "Turkce" for Turkish.</summary>
    public string? NativeName { get; init; }
}
