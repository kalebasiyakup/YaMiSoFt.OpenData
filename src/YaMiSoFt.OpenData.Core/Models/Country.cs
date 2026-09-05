namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// An ISO 3166-1 country and its ITU-T E.164 international calling code. Immutable; loaded
/// once at startup.
/// </summary>
/// <remarks>
/// <see cref="CallingCode"/> is not a unique key. The Nanpa members that are not the United
/// States (e.g. Canada) share the raw "1" country code with no calling-code-level way to tell
/// them apart, and Kazakhstan shares "7" with Russia the same way — both are real ITU
/// assignments, not a data error. <see cref="Iso2"/>/<see cref="Iso3"/> stay the only
/// guaranteed-unique fields, which is why lookups resolve against those instead.
/// </remarks>
public sealed record Country
{
    /// <summary>ISO 3166-1 alpha-2 code, uppercase, e.g. "TR".</summary>
    public required string Iso2 { get; init; }

    /// <summary>ISO 3166-1 alpha-3 code, uppercase, e.g. "TUR".</summary>
    public required string Iso3 { get; init; }

    /// <summary>English name, e.g. "Turkey".</summary>
    public required string NameEn { get; init; }

    /// <summary>Turkish name, e.g. "Türkiye".</summary>
    public required string NameTr { get; init; }

    /// <summary>
    /// ITU-T E.164 international calling code, digits only, no leading '+', e.g. "90". Nanpa
    /// members other than the United States and Canada carry their distinguishing area code
    /// appended (e.g. "1242" for the Bahamas) because the bare "1" would not identify them.
    /// </summary>
    public required string CallingCode { get; init; }
}
