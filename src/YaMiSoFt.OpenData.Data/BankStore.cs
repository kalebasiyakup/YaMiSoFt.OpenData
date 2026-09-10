using System.Collections.Frozen;
using System.Globalization;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;
using YaMiSoFt.OpenData.Core.Validation;

namespace YaMiSoFt.OpenData.Data;

/// <summary>In-memory TCMB payment-system participant lookup, keyed by EFT code (BRD 3.1, Faz 3).</summary>
public sealed class BankStore : IReferenceStore<Bank>
{
    /// <summary>Length of the bank code field inside a Turkish IBAN — one digit wider than the EFT code.</summary>
    private const int IbanBankCodeLength = 5;

    private const int IbanBankCodeOffset = 4;

    private readonly FrozenDictionary<string, Bank> _byCode;
    private readonly SearchIndex<Bank> _index;

    /// <summary>Builds the store from a loaded dataset.</summary>
    /// <exception cref="InvalidDataException">The dataset contains duplicate EFT codes.</exception>
    public BankStore(DataSet<Bank> dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);

        Version = dataset.Version;
        Source = dataset.Source;
        License = dataset.License;

        _byCode = DataIndex.Build(dataset.Items, static bank => bank.Code, "bank EFT code");
        _index = new SearchIndex<Bank>(
            dataset.Items,
            static bank => [bank.Name, bank.LegalName, bank.Code]);
    }

    /// <inheritdoc />
    public string Version { get; }

    /// <summary>Upstream source of the dataset.</summary>
    public string Source { get; }

    /// <summary>SPDX licence identifier of the dataset.</summary>
    public string License { get; }

    /// <summary>Number of participants held.</summary>
    public int Count => _index.Count;

    /// <summary>Every participant, in the order TCMB publishes them.</summary>
    public IReadOnlyList<Bank> All => _index.Items;

    /// <summary>
    /// Resolves a participant by EFT code. The code is accepted in any of the widths it
    /// appears in the wild — "46", "0046" and the five-digit "00046" an IBAN carries all
    /// resolve to the same row — because a caller who has read the digits out of an IBAN
    /// should not have to know which padding this dataset chose.
    /// </summary>
    public Bank? Find(string? code) =>
        NormalizeCode(code) is { } key && _byCode.TryGetValue(key, out var bank) ? bank : null;

    /// <summary>
    /// Resolves the institution that issued <paramref name="iban"/>.
    /// </summary>
    /// <param name="iban">Raw IBAN, spacing and casing as the caller typed it.</param>
    /// <param name="normalized">The IBAN as it was actually read — safe to echo back.</param>
    /// <param name="reason">
    /// Why no bank came back: <see cref="IbanValidator"/>'s own reason when the IBAN does not
    /// validate, "not-turkish-iban" for a well-formed IBAN from another country (the bank code
    /// field is country-specific, so there is nothing to look up), or "unknown-bank-code" when
    /// the digits are read but no participant carries them. Null on success.
    /// </param>
    /// <remarks>
    /// A Turkish IBAN spends its fifth to ninth characters on the bank code, which is the EFT
    /// code left-padded to five digits. Parsing rather than trimming one leading zero is
    /// deliberate: it keeps working if TCMB ever assigns a code that needs the fifth digit.
    /// </remarks>
    public Bank? FindByIban(string? iban, out string normalized, out string? reason)
    {
        if (!IbanValidator.Validate(iban, out normalized, out reason))
        {
            return null;
        }

        if (!normalized.StartsWith("TR", StringComparison.Ordinal))
        {
            reason = "not-turkish-iban";
            return null;
        }

        var field = normalized.AsSpan(IbanBankCodeOffset, IbanBankCodeLength);
        var bank = int.TryParse(field, CultureInfo.InvariantCulture, out var eftCode)
            ? Find(eftCode.ToString("D4", CultureInfo.InvariantCulture))
            : null;

        // A valid checksum over an unassigned bank code is a real possibility — the checksum
        // says the digits were typed correctly, not that they name anyone.
        reason = bank is null ? "unknown-bank-code" : null;
        return bank;
    }

    /// <summary>Filters and orders participants.</summary>
    /// <param name="search">Raw search term. Also matches the EFT code.</param>
    /// <param name="sort">"name", "code" or "type". Defaults to "name".</param>
    /// <param name="direction">Sort direction.</param>
    /// <param name="language">Drives collation; the names themselves are Turkish either way.</param>
    public IReadOnlyList<Bank> Query(
        string? search,
        string? sort,
        SortDirection direction,
        Language language)
    {
        var matches = _index.Filter(search);

        IOrderedEnumerable<Bank> ordered = sort?.ToLowerInvariant() switch
        {
            "code" => ReferenceOrdering.ByText(matches, static bank => bank.Code, direction, Language.English),
            // Secondary key by name: the categories hold up to three dozen rows each, and an
            // unspecified order inside one would reshuffle between requests.
            "type" => ReferenceOrdering
                .ByNumber(matches, static bank => bank.Type, direction)
                .ThenBy(static bank => bank.Name, ReferenceOrdering.ComparerFor(language)),
            _ => ReferenceOrdering.ByText(matches, static bank => bank.Name, direction, language),
        };

        return [.. ordered];
    }

    /// <summary>
    /// Pads an EFT code to the four digits the dataset is keyed on, or null when the input is
    /// not a code at all.
    /// </summary>
    private static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();

        return trimmed.Length is > 0 and <= IbanBankCodeLength &&
               int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value.ToString("D4", CultureInfo.InvariantCulture)
            : null;
    }
}
