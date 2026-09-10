using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data.Tests;

/// <summary>
/// Integrity gate for the bank dataset and the EFT-code lookups built on it (PLAN.md 4, D3).
/// </summary>
/// <remarks>
/// The dataset is a hand-kept transcription of one TCMB list, so what these assert is mostly
/// that the transcription still says what the source said: the row count, the anchor rows a
/// reader can check against the published list in seconds, and the naming rule the
/// <see cref="Bank.Type"/> column was filled in from — including the three rows the rule does
/// not decide, which are the ones a future edit is most likely to get wrong.
/// </remarks>
public sealed class BankDataSetTests
{
    private static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly DataSet<Bank> Banks = DataSetLoader.LoadBanks(DataDirectory);
    private static readonly BankStore Store = new(Banks);

    /// <summary>
    /// The rows whose type the legal name alone does not settle: each is a development and
    /// investment bank whose name says neither "yatırım" nor "kalkınma".
    /// </summary>
    private static readonly string[] UnnamedDevelopmentBanks = ["0004", "0016", "0132"];

    [Fact]
    public void Participant_count_matches_the_published_list()
    {
        // TCMB Ödeme Sistemleri Katılımcıları (2026) lists 71 rows. Pinned rather than ranged:
        // a participant joining or leaving is exactly the change that should force a look at
        // this file, not something to be waved through by a loose bound.
        Assert.Equal(71, Banks.Items.Count);
    }

    [Fact]
    public void Eft_codes_are_four_digits_and_unique()
    {
        var malformed = Banks.Items
            .Where(static bank => bank.Code.Length != 4 || !bank.Code.All(char.IsAsciiDigit))
            .Select(static bank => $"{bank.Code} ({bank.Name})")
            .ToArray();

        Assert.True(malformed.Length == 0, $"Malformed EFT codes: {string.Join(", ", malformed)}");

        var duplicates = Banks.Items
            .GroupBy(static bank => bank.Code, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        Assert.True(duplicates.Length == 0, $"Duplicate EFT codes: {string.Join(", ", duplicates)}");
    }

    [Fact]
    public void Every_row_carries_both_names()
    {
        var missing = Banks.Items
            .Where(static bank =>
                string.IsNullOrWhiteSpace(bank.Name) || string.IsNullOrWhiteSpace(bank.LegalName))
            .Select(static bank => bank.Code)
            .ToArray();

        Assert.True(missing.Length == 0, $"Rows missing a name: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Anchor_rows_match_the_source_list()
    {
        // The codes every Turkish IBAN reader recognises. If a transcription slip ever moves a
        // code onto the wrong institution, it shows up here first.
        Assert.Equal("T.C. ZİRAAT BANKASI A.Ş.", Store.Find("0010")!.LegalName);
        Assert.Equal("T. İŞ BANKASI A.Ş.", Store.Find("0064")!.LegalName);
        Assert.Equal("T. GARANTİ BANKASI A.Ş.", Store.Find("0062")!.LegalName);
        Assert.Equal("AKBANK T.A.Ş.", Store.Find("0046")!.LegalName);
        Assert.Equal("YAPI VE KREDİ BANKASI A.Ş.", Store.Find("0067")!.LegalName);
        Assert.Equal("T. VAKIFLAR BANKASI T.A.O.", Store.Find("0015")!.LegalName);
    }

    [Fact]
    public void Participation_banks_are_exactly_the_ones_named_so()
    {
        foreach (var bank in Banks.Items)
        {
            var namedParticipation = bank.LegalName.Contains("KATILIM", StringComparison.Ordinal);

            Assert.Equal(
                namedParticipation,
                bank.Type == BankType.Participation);
        }
    }

    [Fact]
    public void Development_banks_are_the_ones_named_so_plus_three_that_are_not()
    {
        foreach (var bank in Banks.Items)
        {
            var namedDevelopment =
                bank.LegalName.Contains("YATIRIM", StringComparison.Ordinal) ||
                bank.LegalName.Contains("KALK", StringComparison.Ordinal) ||
                UnnamedDevelopmentBanks.Contains(bank.Code, StringComparer.Ordinal);

            Assert.Equal(
                namedDevelopment,
                bank.Type == BankType.DevelopmentInvestment);
        }
    }

    [Fact]
    public void One_central_bank_and_two_non_banks()
    {
        Assert.Equal("0001", Assert.Single(Banks.Items, static bank => bank.Type == BankType.CentralBank).Code);

        var other = Banks.Items.Where(static bank => bank.Type == BankType.Other).Select(static bank => bank.Code);
        Assert.Equal(new[] { "0806", "0807" }, other.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("0010")]
    [InlineData("10")]
    [InlineData("00010")]
    [InlineData(" 0010 ")]
    public void Eft_code_resolves_at_any_width(string code) =>
        Assert.Equal("Ziraat Bankası", Store.Find(code)?.Name);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abcd")]
    [InlineData("-10")]
    [InlineData("000010")]
    [InlineData("9999")]
    public void Non_codes_and_unassigned_codes_resolve_to_nothing(string? code) =>
        Assert.Null(Store.Find(code));

    [Fact]
    public void Iban_resolves_to_the_bank_holding_its_eft_code()
    {
        var bank = Store.FindByIban("TR42 0001 0000 0000 0000 0000 01", out var normalized, out var reason);

        Assert.Equal("0010", bank?.Code);
        Assert.Equal("TR420001000000000000000001", normalized);
        Assert.Null(reason);
    }

    [Fact]
    public void Iban_lookup_reports_why_it_found_nothing()
    {
        // Valid checksum, but 0061 is not an assigned EFT code — this is the sample IBAN the
        // BRD itself uses, which is exactly why the endpoint must not answer it with a bank.
        Assert.Null(Store.FindByIban("TR330006100519786457841326", out _, out var unknown));
        Assert.Equal("unknown-bank-code", unknown);

        // A real German IBAN: well-formed, but its fifth to ninth characters are not an EFT code.
        Assert.Null(Store.FindByIban("DE89370400440532013000", out _, out var foreign));
        Assert.Equal("not-turkish-iban", foreign);

        Assert.Null(Store.FindByIban("TR420001000000000000000002", out _, out var checksum));
        Assert.Equal("invalid-checksum", checksum);

        Assert.Null(Store.FindByIban("not an iban", out _, out var format));
        Assert.Equal("invalid-length", format);
    }

    [Fact]
    public void Query_sorts_by_type_then_name()
    {
        var ordered = Store.Query(null, "type", SortDirection.Ascending, Language.Turkish);

        Assert.Equal(Banks.Items.Count, ordered.Count);
        Assert.Equal(BankType.Deposit, ordered[0].Type);
        Assert.Equal(BankType.Other, ordered[^1].Type);

        // Ties inside a category are ordered, not incidental.
        var participation = ordered.Where(static bank => bank.Type == BankType.Participation).ToArray();
        Assert.Equal(
            participation.Select(static bank => bank.Name).Order(ReferenceOrdering.ComparerFor(Language.Turkish)),
            participation.Select(static bank => bank.Name));
    }

    [Fact]
    public void Banks_are_searchable_by_legal_name_and_code()
    {
        Assert.Contains(
            Store.Query("kuveyt", null, SortDirection.Ascending, Language.Turkish),
            static bank => bank.Code == "0205");

        Assert.Contains(
            Store.Query("0111", null, SortDirection.Ascending, Language.Turkish),
            static bank => bank.Name == "QNB");
    }
}
