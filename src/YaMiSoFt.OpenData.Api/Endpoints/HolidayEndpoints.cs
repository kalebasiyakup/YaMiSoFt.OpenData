using System.Collections.Frozen;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Api.RateLimiting;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>Turkish public holiday endpoints (BRD 5.2).</summary>
public static class HolidayEndpoints
{
    private static readonly FrozenSet<string> HolidayFields = new[]
    {
        "date", "nametr", "nameen", "kind", "ishalfday", "year",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps the holiday routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapHolidayEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var holidays = group.MapGroup("/holidays").WithTags("Public Holidays");

        holidays.MapGet("/all", GetAll)
            .WithName("DownloadHolidays")
            .WithBilingualSummary(
                "Downloads every covered year in a single response.",
                "Kapsanan tüm yılları tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        holidays.MapGet("/{year:int}", GetYear)
            .WithName("GetHolidaysForYear")
            .WithBilingualSummary(
                "Lists Turkey's official non-working days for one year.",
                "Bir yıl için Türkiye'nin resmi tatil günlerini listeler.");

        return group;
    }

    private static IResult GetYear(
        HttpContext context,
        HolidayStore store,
        IOptions<OpenDataOptions> options,
        int year,
        [AsParameters] ListQuery query,
        [FromQuery] string? country = null)
    {
        if (CountryProblem(context, country) is { } countryError)
        {
            return countryError;
        }

        if (!ListRequest.TryParse(context, query, HolidayFields, out var request, out var error))
        {
            return error!;
        }

        if (!store.Covers(year))
        {
            // A 404 would suggest the year does not exist; it does, the dataset just stops.
            // Naming the range tells the caller both what went wrong and what to ask for.
            return ApiProblem.Create(
                context,
                StatusCodes.Status400BadRequest,
                "year-out-of-range",
                "Year not covered",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Holidays are published for {store.FirstYear}-{store.LastYear}; {year} is outside that range."));
        }

        return new DataResult<IReadOnlyList<Holiday>>(
            store.ForYear(year),
            OpenDataJson.TypeInfo<IReadOnlyList<Holiday>>(),
            request.Fields,
            store.Version,
            $"holidays={year}&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    private static IResult GetAll(
        HttpContext context,
        HolidayStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query,
        [FromQuery] string? country = null)
    {
        if (CountryProblem(context, country) is { } countryError)
        {
            return countryError;
        }

        if (!ListRequest.TryParse(context, query, HolidayFields, out var request, out var error))
        {
            return error!;
        }

        return new DataResult<IReadOnlyList<Holiday>>(
            store.All,
            OpenDataJson.TypeInfo<IReadOnlyList<Holiday>>(),
            request.Fields,
            store.Version,
            $"holidays-all&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    /// <summary>
    /// Rejects a country the dataset does not cover.
    /// </summary>
    /// <remarks>
    /// The parameter is accepted even though Turkey is the only answer, because the BRD's
    /// endpoint shape includes it and because silently ignoring it would be worse: a caller
    /// asking for German holidays would receive Turkish ones and have no way to notice.
    /// </remarks>
    private static IResult? CountryProblem(HttpContext context, string? country)
    {
        if (string.IsNullOrWhiteSpace(country) ||
            country.Equals("TR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ApiProblem.Create(
            context,
            StatusCodes.Status400BadRequest,
            "country-not-supported",
            "Country not supported",
            $"Holidays are published for Turkey only; '{EchoedInput.Clip(country)}' is not covered.");
    }
}
