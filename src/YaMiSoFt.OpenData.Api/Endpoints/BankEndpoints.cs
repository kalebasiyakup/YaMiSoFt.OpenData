using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Api.RateLimiting;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>Turkish banking reference endpoints (BRD 5.2, Faz 3).</summary>
public static class BankEndpoints
{
    private static readonly FrozenSet<string> BankFields = new[]
    {
        "code", "name", "legalname", "type",
    }.ToFrozenSet(StringComparer.Ordinal);

    private const string CodeHint =
        "Use a TCMB EFT code such as '0010' (Ziraat Bankası); '10' and the five-digit '00010' "
        + "an IBAN carries are accepted too.";

    /// <summary>Maps the bank routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapBankEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var banks = group.MapGroup("/banks").WithTags("Banks");

        banks.MapGet("/", (
                HttpContext context,
                BankStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.List(context, store, options.Value.Cache, query, BankFields, "banks"))
            .WithName("ListBanks")
            .WithBilingualSummary(
                "Lists the banks and institutions in TCMB's payment systems with their EFT codes.",
                "TCMB ödeme sistemlerindeki banka ve kurumları EFT kodlarıyla listeler.");

        banks.MapGet("/all", (
                HttpContext context,
                BankStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.All(context, store, options.Value.Cache, query, BankFields, "banks"))
            .WithName("DownloadBanks")
            .WithBilingualSummary(
                "Downloads every bank in a single response.",
                "Tüm bankaları tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        // Registered before "/{code}" for readability only — a literal segment already wins
        // over a route parameter regardless of order, the same way "/all" above does.
        banks.MapGet("/by-iban/{iban}", (
                HttpContext context,
                BankStore store,
                IOptions<OpenDataOptions> options,
                string iban,
                [AsParameters] ListQuery query) =>
            ByIban(context, store, options.Value.Cache, query, iban))
            .WithName("GetBankByIban")
            .WithBilingualSummary(
                "Resolves the bank that issued a Turkish IBAN, from the EFT code inside it.",
                "Bir Türk IBAN'ının içindeki EFT kodundan bankayı bulur.");

        banks.MapGet("/{code}", (
                HttpContext context,
                BankStore store,
                IOptions<OpenDataOptions> options,
                string code,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.Single(
                context, store, options.Value.Cache, query, BankFields, code, "bank", CodeHint))
            .WithName("GetBank")
            .WithBilingualSummary(
                "Gets one bank by its TCMB EFT code.",
                "TCMB EFT koduna göre bir bankayı getirir.");

        return group;
    }

    /// <summary>
    /// Resolves the bank whose EFT code is embedded in <paramref name="iban"/>.
    /// </summary>
    /// <remarks>
    /// This answers a different question from <c>/validate/iban</c>, and the status codes say
    /// so. There, "is this IBAN valid?" is a question a 200 can answer with "no"; here the
    /// IBAN is a resource identifier, so one that is not an IBAN at all is a bad request (400)
    /// and one that names no participant is a miss (404). A caller that wants the soft answer
    /// still has the validation endpoint.
    ///
    /// The IBAN travels in the URL, so it lands in access logs and in edge cache keys like any
    /// other path. Nothing here needs the account number — a caller who would rather not put a
    /// customer's full IBAN through a CDN can read characters 5-9 out of it and call
    /// <c>/banks/{code}</c> instead, which returns the same record.
    /// </remarks>
    private static IResult ByIban(
        HttpContext context,
        BankStore store,
        CacheOptions cache,
        ListQuery query,
        string iban)
    {
        if (!ListRequest.TryParse(context, query, BankFields, out var request, out var error))
        {
            return error!;
        }

        var bank = store.FindByIban(iban, out var normalized, out var reason);
        var echoed = EchoedInput.Clip(normalized);

        if (bank is null)
        {
            return reason switch
            {
                "not-turkish-iban" => ApiProblem.NotFound(
                    context,
                    "bank-not-found",
                    "Bank not found",
                    $"'{echoed}' is a valid IBAN but not a Turkish one, and the bank code field "
                    + "is country-specific. This dataset only resolves IBANs starting with 'TR'."),
                "unknown-bank-code" => ApiProblem.NotFound(
                    context,
                    "bank-not-found",
                    "Bank not found",
                    $"'{echoed}' is a well-formed Turkish IBAN, but no TCMB participant carries "
                    + $"its bank code. {CodeHint}"),
                _ => ApiProblem.InvalidQuery(
                    context,
                    $"'{echoed}' is not a valid IBAN ({reason}). Check it with "
                    + "/api/v1/validate/iban to see which part failed."),
            };
        }

        return new DataResult<Bank>(
            bank,
            OpenDataJson.TypeInfo<Bank>(),
            request.Fields,
            store.Version,
            // The validator is keyed on the resolved code, not the IBAN, so every account at
            // one bank shares an ETag — a client revalidating a stored answer gets its 304
            // even when it asks about a different account.
            $"bank-by-iban={bank.Code}&fields={request.Fields.CanonicalKey}",
            cache.Default);
    }
}
