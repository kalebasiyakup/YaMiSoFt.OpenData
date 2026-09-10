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
/// Currency, language and country endpoints (BRD 5.2).
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

    private static readonly FrozenSet<string> CountryFields = new[]
    {
        "iso2", "iso3", "nameen", "nametr", "callingcode",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps the currency and language routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapReferenceEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var currencies = group.MapGroup("/currencies").WithTags("Currencies");

        currencies.MapGet("/", (
                HttpContext context,
                CurrencyStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.List(context, store, options.Value.Cache, query, CurrencyFields, "currencies"))
            .WithName("ListCurrencies")
            .WithBilingualSummary(
                "Lists ISO 4217 currencies with symbols and minor-unit counts.",
                "Para birimlerini, sembolleri ve kuruş basamak sayılarıyla birlikte listeler.");

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
                "Para birimi koduna (örn. TRY) göre bir para birimini getirir.");

        var languages = group.MapGroup("/languages").WithTags("Languages");

        languages.MapGet("/", (
                HttpContext context,
                LanguageStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.List(context, store, options.Value.Cache, query, LanguageFields, "languages"))
            .WithName("ListLanguages")
            .WithBilingualSummary(
                "Lists ISO 639 languages with native names.",
                "Dilleri, kendi dillerindeki adlarıyla birlikte listeler.");

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
                "İki veya üç harfli dil koduna (örn. tr) göre bir dili getirir.");

        var countries = group.MapGroup("/countries").WithTags("Countries");

        countries.MapGet("/", (
                HttpContext context,
                CountryStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.List(context, store, options.Value.Cache, query, CountryFields, "countries"))
            .WithName("ListCountries")
            .WithBilingualSummary(
                "Lists ISO 3166-1 countries with their international calling codes.",
                "Ülkeleri, uluslararası telefon kodlarıyla birlikte listeler.");

        countries.MapGet("/all", (
                HttpContext context,
                CountryStore store,
                IOptions<OpenDataOptions> options,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.All(context, store, options.Value.Cache, query, CountryFields, "countries"))
            .WithName("DownloadCountries")
            .WithBilingualSummary(
                "Downloads every country in a single response.",
                "Tüm ülkeleri tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        countries.MapGet("/{code}", (
                HttpContext context,
                CountryStore store,
                IOptions<OpenDataOptions> options,
                string code,
                [AsParameters] ListQuery query) =>
            ReferenceHandlers.Single(
                context, store, options.Value.Cache, query, CountryFields, code,
                "country", "Use an ISO 3166-1 alpha-2 or alpha-3 code such as 'TR' or 'TUR'."))
            .WithName("GetCountry")
            .WithBilingualSummary(
                "Gets one country by ISO 3166-1 alpha-2 or alpha-3 code.",
                "İki veya üç harfli ülke koduna (örn. TR ya da TUR) göre bir ülkeyi getirir.");

        return group;
    }
}
