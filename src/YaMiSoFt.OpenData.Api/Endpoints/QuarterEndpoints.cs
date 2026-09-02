using System.Collections.Frozen;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Api.RateLimiting;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>
/// Quarter (semt) and postal code endpoints — the level between a district and its
/// settlements (BRD 5.2).
/// </summary>
/// <remarks>
/// Postal codes are served from here rather than from the settlement endpoints because that
/// is where they are assigned: one code, one quarter. Answering a code lookup from 2,433
/// resident rows instead of the lazily loaded 73,552-row settlement file is the difference
/// between a lookup that is always fast and one that pays a cold-start parse.
/// </remarks>
public static class QuarterEndpoints
{
    private static readonly FrozenSet<string> Fields = new[]
    {
        "id", "name", "nameupper", "slug", "provinceid", "provincename", "districtid",
        "districtname", "postalcode", "settlementcount",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps the quarter and postal code routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapQuarterEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var quarters = group.MapGroup("/quarters").WithTags("Turkey");

        quarters.MapGet("/", GetQuarters)
            .WithName("ListQuarters")
            .WithBilingualSummary(
                "Lists Turkish quarters (semt). Filter by province or district.",
                "Türkiye semtlerini listeler. İle veya ilçeye göre filtrelenir.");

        quarters.MapGet("/all", GetAllQuarters)
            .WithName("DownloadQuarters")
            .WithBilingualSummary(
                "Downloads every quarter, with its postal code, in a single response.",
                "Tüm semtleri, posta kodlarıyla birlikte, tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        quarters.MapGet("/{id:int}", GetQuarter)
            .WithName("GetQuarter")
            .WithBilingualSummary(
                "Gets one quarter by identifier.",
                "Kimliğine göre bir semti getirir.");

        quarters.MapGet("/{id:int}/neighborhoods", GetQuarterNeighborhoods)
            .WithName("ListQuarterNeighborhoods")
            .WithBilingualSummary(
                "Lists the neighbourhoods and villages of one quarter.",
                "Bir semtin mahallelerini ve köylerini listeler.");

        group.MapGet("/districts/{districtId:int}/quarters", GetDistrictQuarters)
            .WithName("ListDistrictQuarters")
            .WithTags("Turkey")
            .WithBilingualSummary(
                "Lists the quarters of one district.",
                "Bir ilçenin semtlerini listeler.");

        group.MapGet("/postal-codes/{postalCode}", GetByPostalCode)
            .WithName("GetPostalCode")
            .WithTags("Turkey")
            .WithBilingualSummary(
                "Gets the quarter a five-digit postal code is assigned to.",
                "Beş haneli bir posta kodunun atandığı semti getirir.");

        group.MapGet("/postal-codes/{postalCode}/neighborhoods", GetPostalCodeNeighborhoods)
            .WithName("ListPostalCodeNeighborhoods")
            .WithTags("Turkey")
            .WithBilingualSummary(
                "Lists the settlements covered by one postal code.",
                "Bir posta kodunun kapsadığı yerleşim yerlerini listeler.");

        return group;
    }

    private static IResult GetQuarters(
        HttpContext context,
        QuarterStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query,
        [FromQuery] int? provinceId = null,
        [FromQuery] int? districtId = null)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        var matches = store.Query(
            request.Search, request.Sort, request.Direction, request.Language, provinceId, districtId);

        return Paged(store, request, matches, options.Value.Cache, $"quarters&p={provinceId}&d={districtId}");
    }

    private static IResult GetAllQuarters(
        HttpContext context,
        QuarterStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        return new DataResult<IReadOnlyList<Quarter>>(
            store.All,
            OpenDataJson.TypeInfo<IReadOnlyList<Quarter>>(),
            request.Fields,
            store.Version,
            $"quarters-all&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    private static IResult GetQuarter(
        HttpContext context,
        QuarterStore store,
        IOptions<OpenDataOptions> options,
        int id,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        var quarter = store.Find(id);

        if (quarter is null)
        {
            return QuarterNotFound(context, $"No quarter matches the identifier '{id}'.");
        }

        return Single(quarter, request, store.Version, options.Value.Cache);
    }

    private static IResult GetDistrictQuarters(
        HttpContext context,
        QuarterStore store,
        TurkeyStore turkey,
        IOptions<OpenDataOptions> options,
        int districtId,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        // Resolved against the district list so an unknown district is a 404 rather than an
        // empty page, which would read as "this district has no quarters".
        if (turkey.FindDistrict(districtId) is null)
        {
            return ApiProblem.NotFound(context, "district-not-found", "District not found",
                $"No district matches the identifier '{districtId}'.");
        }

        var matches = request.IsSearch || request.Sort is not null
            ? store.Query(request.Search, request.Sort, request.Direction, request.Language, districtId: districtId)
            : store.OfDistrict(districtId);

        return Paged(store, request, matches, options.Value.Cache, $"district={districtId}&quarters");
    }

    private static IResult GetByPostalCode(
        HttpContext context,
        QuarterStore store,
        IOptions<OpenDataOptions> options,
        string postalCode,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        var quarter = store.FindByPostalCode(postalCode);

        if (quarter is null)
        {
            return PostalCodeNotFound(context, postalCode);
        }

        return Single(quarter, request, store.Version, options.Value.Cache);
    }

    private static IResult GetQuarterNeighborhoods(
        HttpContext context,
        QuarterStore store,
        NeighborhoodStore settlements,
        IOptions<OpenDataOptions> options,
        int id,
        [AsParameters] ListQuery query)
    {
        if (store.Find(id) is null)
        {
            return QuarterNotFound(context, $"No quarter matches the identifier '{id}'.");
        }

        return NeighborhoodEndpoints.PagedForQuarter(context, settlements, options, id, query, $"quarter={id}");
    }

    private static IResult GetPostalCodeNeighborhoods(
        HttpContext context,
        QuarterStore store,
        NeighborhoodStore settlements,
        IOptions<OpenDataOptions> options,
        string postalCode,
        [AsParameters] ListQuery query)
    {
        var quarter = store.FindByPostalCode(postalCode);

        if (quarter is null)
        {
            return PostalCodeNotFound(context, postalCode);
        }

        return NeighborhoodEndpoints.PagedForQuarter(
            context, settlements, options, quarter.Id, query, $"postal={quarter.PostalCode}");
    }

    private static IResult Single(
        Quarter quarter,
        ListRequest request,
        string version,
        CacheOptions cache) =>
        new DataResult<Quarter>(
            quarter,
            OpenDataJson.TypeInfo<Quarter>(),
            request.Fields,
            version,
            $"quarter={quarter.Id}&fields={request.Fields.CanonicalKey}",
            cache.Default);

    private static IResult Paged(
        QuarterStore store,
        ListRequest request,
        IReadOnlyList<Quarter> matches,
        CacheOptions cache,
        string keyPrefix) =>
        new DataResult<PagedResult<Quarter>>(
            PagedResult<Quarter>.Create(matches, request.Page),
            OpenDataJson.TypeInfo<PagedResult<Quarter>>(),
            request.Fields,
            store.Version,
            $"{keyPrefix}&{request.CanonicalKey}",
            request.IsSearch ? cache.Search : cache.Default,
            itemsProperty: "items");

    private static IResult QuarterNotFound(HttpContext context, string detail) =>
        ApiProblem.NotFound(context, "quarter-not-found", "Quarter not found", detail);

    private static IResult PostalCodeNotFound(HttpContext context, string postalCode) =>
        ApiProblem.NotFound(context, "postal-code-not-found", "Postal code not found",
            $"No quarter uses the postal code '{EchoedInput.Clip(postalCode)}'. "
            + "Turkish postal codes are five digits.");
}
