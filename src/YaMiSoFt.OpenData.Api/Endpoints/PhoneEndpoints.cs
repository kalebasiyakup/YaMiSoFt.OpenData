using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Api.RateLimiting;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>Turkish telecom reference endpoints (BRD 5.2).</summary>
public static class PhoneEndpoints
{
    private static readonly FrozenSet<string> MobileOperatorFields = new[]
    {
        "code", "name", "legalname",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps the mobile operator routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapPhoneEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var operators = group.MapGroup("/mobile-operators").WithTags("Turkey");

        operators.MapGet("/", (
                HttpContext context,
                MobileOperatorStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.List(context, store, options.Value.Cache, query, MobileOperatorFields, "mobile-operators"))
            .WithName("ListMobileOperators")
            .WithBilingualSummary(
                "Lists Turkey's licensed mobile network operators.",
                "Türkiye'nin ruhsatlı GSM operatörlerini listeler.");

        operators.MapGet("/all", (
                HttpContext context,
                MobileOperatorStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.All(context, store, options.Value.Cache, query, MobileOperatorFields, "mobile-operators"))
            .WithName("DownloadMobileOperators")
            .WithBilingualSummary(
                "Downloads every mobile operator in a single response.",
                "Tüm GSM operatörlerini tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        operators.MapGet("/{code}", (
                HttpContext context,
                MobileOperatorStore store,
                IOptions<OpenDataOptions> options,
                string code,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.Single(
                context, store, options.Value.Cache, query, MobileOperatorFields, code,
                "mobile operator", "Use a slug such as 'turkcell', 'vodafone' or 'turk-telekom'."))
            .WithName("GetMobileOperator")
            .WithBilingualSummary(
                "Gets one mobile operator by its slug code.",
                "Slug koduna göre bir GSM operatörünü getirir.");

        return group;
    }
}
