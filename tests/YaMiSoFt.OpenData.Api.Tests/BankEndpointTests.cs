using System.Net;
using System.Text.Json;

namespace YaMiSoFt.OpenData.Api.Tests;

/// <summary>Covers the bank endpoints, including the IBAN lookup (BRD 5.2, Faz 3).</summary>
public sealed class BankEndpointTests(OpenDataApiFactory factory) : IClassFixture<OpenDataApiFactory>
{
    /// <summary>A syntactically valid Turkish IBAN carrying Ziraat Bankası's EFT code, 0010.</summary>
    private const string ZiraatIban = "TR420001000000000000000001";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Bank_resolves_by_its_eft_code()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/banks/0010", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var bank = await ReadJsonAsync(response);
        Assert.Equal("0010", bank.GetProperty("code").GetString());
        Assert.Equal("Ziraat Bankası", bank.GetProperty("name").GetString());
        Assert.Equal("Deposit", bank.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("10")]
    [InlineData("00010")]
    public async Task Eft_code_is_accepted_at_the_widths_it_appears_in(string code)
    {
        using var response = await _client.GetAsync(new Uri($"/api/v1/banks/{code}", UriKind.Relative));

        response.EnsureSuccessStatusCode();
        Assert.Equal("0010", (await ReadJsonAsync(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unknown_code_returns_problem_details()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/banks/9999", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(
            "bank-not-found",
            (await ReadJsonAsync(response)).GetProperty("type").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Iban_lookup_names_the_issuing_bank()
    {
        using var response = await _client.GetAsync(
            new Uri($"/api/v1/banks/by-iban/{ZiraatIban}", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var bank = await ReadJsonAsync(response);
        Assert.Equal("0010", bank.GetProperty("code").GetString());
        Assert.Equal("T.C. ZİRAAT BANKASI A.Ş.", bank.GetProperty("legalName").GetString());
    }

    [Fact]
    public async Task Iban_lookup_shares_one_validator_across_a_banks_accounts()
    {
        // Two different accounts at the same bank return the same record, so they must share
        // an ETag — that is what lets a client revalidate instead of refetching.
        using var first = await _client.GetAsync(
            new Uri($"/api/v1/banks/by-iban/{ZiraatIban}", UriKind.Relative));
        using var second = await _client.GetAsync(
            new Uri("/api/v1/banks/by-iban/TR020001000000000000000042", UriKind.Relative));

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();

        Assert.NotNull(first.Headers.ETag);
        Assert.Equal(first.Headers.ETag!.Tag, second.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task A_foreign_iban_is_a_miss_not_an_error()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/banks/by-iban/DE89370400440532013000", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await ReadJsonAsync(response);
        Assert.Contains("bank-not-found", problem.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.Contains("Turkish", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_valid_iban_on_an_unassigned_code_is_a_miss()
    {
        // The BRD's own sample IBAN: the checksum passes, but 0061 names no participant.
        using var response = await _client.GetAsync(
            new Uri("/api/v1/banks/by-iban/TR330006100519786457841326", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_iban_is_a_bad_request()
    {
        // Unlike /validate/iban, which answers "is this valid?" with a 200 and isValid:false,
        // here the IBAN is the resource identifier — a broken one cannot identify anything.
        using var response = await _client.GetAsync(
            new Uri("/api/v1/banks/by-iban/TR420001000000000000000002", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "invalid-checksum",
            (await ReadJsonAsync(response)).GetProperty("detail").GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Downloading_all_banks_returns_the_whole_participant_list()
    {
        using var response = await _client.GetAsync(new Uri("/api/v1/banks/all", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).EnumerateArray().ToArray();

        Assert.Equal(71, items.Length);
        Assert.Contains(items, item => item.GetProperty("code").GetString() == "0205");
    }

    [Fact]
    public async Task Banks_are_searchable_diacritic_insensitively()
    {
        using var response = await _client.GetAsync(
            new Uri("/api/v1/banks?search=turkiye%20finans", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var items = (await ReadJsonAsync(response)).GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal("0206", Assert.Single(items).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Field_selection_applies_to_the_iban_lookup_too()
    {
        using var response = await _client.GetAsync(
            new Uri($"/api/v1/banks/by-iban/{ZiraatIban}?fields=code", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        var bank = await ReadJsonAsync(response);
        Assert.Equal("0010", bank.GetProperty("code").GetString());
        Assert.False(bank.TryGetProperty("name", out _));
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
