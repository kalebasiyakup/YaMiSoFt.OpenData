using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Api.RateLimiting;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>
/// Currency and language endpoints (BRD 5.2).
/// </summary>
/// <remarks>
/// Each dataset contributes only its field list, its code hint and three thin lambdas; the
/// handler bodies live in <see cref="ReferenceHandlers"/> so all of them validate, cache and
/// fail identically.
/// </remarks>
public static class ReferenceEndpoints
{
    private static readonly FrozenSet<string> CurrencyFields = new[]
    {
        "code", "nameen", "nametr", "symbol", "decimaldigits",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> LanguageFields = new[]
    {
        "alpha2", "alpha3", "nameen", "nametr", "nativename",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps the currency and language routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapReferenceEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var currencies = group.MapGroup("/currencies").WithTags("Reference");

        currencies.MapGet("/", (
                HttpContext context,
                CurrencyStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.List(context, store, options.Value.Cache, query, CurrencyFields, "currencies"))
            .WithName("ListCurrencies")
            .WithBilingualSummary(
                "Lists ISO 4217 currencies with symbols and minor-unit counts.",
                "Sembolleri ve kuruş basamak sayılarıyla ISO 4217 para birimlerini listeler.");

        currencies.MapGet("/all", (
                HttpContext context,
                CurrencyStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.All(context, store, options.Value.Cache, query, CurrencyFields, "currencies"))
            .WithName("DownloadCurrencies")
            .WithBilingualSummary(
                "Downloads every currency in a single response.",
                "Tüm para birimlerini tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        currencies.MapGet("/{code}", (
                HttpContext context,
                CurrencyStore store,
                IOptions<OpenDataOptions> options,
                string code,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.Single(
                context, store, options.Value.Cache, query, CurrencyFields, code,
                "currency", "Use an ISO 4217 code such as 'TRY'."))
            .WithName("GetCurrency")
            .WithBilingualSummary(
                "Gets one currency by ISO 4217 code.",
                "ISO 4217 koduna göre bir para birimini getirir.");

        var languages = group.MapGroup("/languages").WithTags("Reference");

        languages.MapGet("/", (
                HttpContext context,
                LanguageStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.List(context, store, options.Value.Cache, query, LanguageFields, "languages"))
            .WithName("ListLanguages")
            .WithBilingualSummary(
                "Lists ISO 639 languages with native names.",
                "Yerel adlarıyla ISO 639 dillerini listeler.");

        languages.MapGet("/all", (
                HttpContext context,
                LanguageStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.All(context, store, options.Value.Cache, query, LanguageFields, "languages"))
            .WithName("DownloadLanguages")
            .WithBilingualSummary(
                "Downloads every language in a single response.",
                "Tüm dilleri tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        languages.MapGet("/{code}", (
                HttpContext context,
                LanguageStore store,
                IOptions<OpenDataOptions> options,
                string code,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.Single(
                context, store, options.Value.Cache, query, LanguageFields, code,
                "language", "Use an ISO 639-1 or 639-2 code such as 'tr'."))
            .WithName("GetLanguage")
            .WithBilingualSummary(
                "Gets one language by ISO 639-1 or 639-2 code.",
                "ISO 639-1 veya 639-2 koduna göre bir dili getirir.");

        return group;
    }
}
