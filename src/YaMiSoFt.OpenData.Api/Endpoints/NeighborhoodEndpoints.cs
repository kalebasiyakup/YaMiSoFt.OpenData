using System.Collections.Frozen;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Api.OpenApi;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Core.Models;
using YaMiSoFt.OpenData.Core.Querying;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>Neighbourhood and village endpoints (BRD 5.2).</summary>
/// <remarks>
/// Postal code lookup lives in <see cref="QuarterEndpoints"/>, not here: a code is assigned to
/// a quarter, and answering from the quarter file avoids loading this one.
/// </remarks>
public static class NeighborhoodEndpoints
{
    private static readonly FrozenSet<string> Fields = new[]
    {
        "id", "name", "slug", "kind", "provinceid", "districtid", "quarterid",
        "postalcode", "villagename",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps the settlement routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapNeighborhoodEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var neighborhoods = group.MapGroup("/neighborhoods").WithTags("Address");

        neighborhoods.MapGet("/", GetNeighborhoods)
            .WithName("ListNeighborhoods")
            .WithBilingualSummary(
                "Lists Turkish neighbourhoods and villages. Filter by province, district, "
                + "quarter or kind.",
                "Türkiye mahallelerini ve köylerini listeler. İle, ilçeye, semte veya türe göre "
                + "filtrelenir.");

        neighborhoods.MapGet("/{id:int}", GetNeighborhood)
            .WithName("GetNeighborhood")
            .WithBilingualSummary(
                "Gets one neighbourhood or village by identifier.",
                "Kimliğine göre bir mahalle veya köyü getirir.");

        // No /all here on purpose: the dataset is tens of megabytes, which is a download
        // rather than a response. Callers who want everything should take the file from the
        // repository.
        group.MapGet("/districts/{districtId:int}/neighborhoods", GetDistrictNeighborhoods)
            .WithName("ListDistrictNeighborhoods")
            .WithTags("Address")
            .WithBilingualSummary(
                "Lists the neighbourhoods and villages of one district.",
                "Bir ilçenin mahallelerini ve köylerini listeler.");

        return group;
    }

    private static IResult GetNeighborhoods(
        HttpContext context,
        NeighborhoodStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query,
        [FromQuery] int? provinceId = null,
        [FromQuery] int? districtId = null,
        [FromQuery] int? quarterId = null,
        [FromQuery] string? kind = null)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        if (!TryParseKind(kind, out var settlementKind))
        {
            return ApiProblem.InvalidQuery(
                context,
                $"Unknown kind '{EchoedInput.Clip(kind)}'. Use 'neighborhood' or 'village'.");
        }

        // An unfiltered list of 73,552 rows is nobody's intent, and paging through it a page at
        // a time is the scraping pattern the bulk endpoints exist to prevent elsewhere.
        if (provinceId is null && districtId is null && quarterId is null && !request.IsSearch)
        {
            return ApiProblem.InvalidQuery(
                context,
                "Narrow the request with 'provinceId', 'districtId', 'quarterId' or 'search'. "
                + "The full settlement list is published as a file in the repository.");
        }

        var matches = store.Query(
            request.Search, request.Sort, request.Direction, request.Language,
            provinceId, districtId, quarterId, settlementKind);

        return Paged(
            store, request, matches, options.Value.Cache,
            $"neighborhoods&p={provinceId}&d={districtId}&q={quarterId}&k={kind}");
    }

    private static IResult GetNeighborhood(
        HttpContext context,
        NeighborhoodStore store,
        IOptions<OpenDataOptions> options,
        int id,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        var settlement = store.Find(id);

        if (settlement is null)
        {
            return ApiProblem.NotFound(context, "neighborhood-not-found", "Settlement not found",
                $"No neighbourhood or village matches the identifier '{id}'.");
        }

        return new DataResult<Neighborhood>(
            settlement,
            OpenDataJson.TypeInfo<Neighborhood>(),
            request.Fields,
            store.Version,
            $"neighborhood={id}&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    private static IResult GetDistrictNeighborhoods(
        HttpContext context,
        NeighborhoodStore store,
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
        // empty page, which would read as "this district has no settlements".
        if (turkey.FindDistrict(districtId) is null)
        {
            return ApiProblem.NotFound(context, "district-not-found", "District not found",
                $"No district matches the identifier '{districtId}'.");
        }

        var matches = request.IsSearch || request.Sort is not null
            ? store.Query(request.Search, request.Sort, request.Direction, request.Language, districtId: districtId)
            : store.OfDistrict(districtId);

        return Paged(store, request, matches, options.Value.Cache, $"district={districtId}&neighborhoods");
    }

    /// <summary>
    /// Lists the settlements of one quarter. Exposed to <see cref="QuarterEndpoints"/> so the
    /// quarter and postal code routes serve the same shape, with the same field validation,
    /// as the settlement routes here.
    /// </summary>
    internal static IResult PagedForQuarter(
        HttpContext context,
        NeighborhoodStore store,
        IOptions<OpenDataOptions> options,
        int quarterId,
        ListQuery query,
        string keyPrefix)
    {
        if (!ListRequest.TryParse(context, query, Fields, out var request, out var error))
        {
            return error!;
        }

        var matches = request.IsSearch || request.Sort is not null
            ? store.Query(request.Search, request.Sort, request.Direction, request.Language, quarterId: quarterId)
            : store.OfQuarter(quarterId);

        return Paged(store, request, matches, options.Value.Cache, keyPrefix);
    }

    private static bool TryParseKind(string? value, out SettlementKind? kind)
    {
        kind = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "neighborhood":
            case "neighbourhood":
            case "mahalle":
                kind = SettlementKind.Neighborhood;
                return true;
            case "village":
            case "koy":
                kind = SettlementKind.Village;
                return true;
            default:
                return false;
        }
    }

    private static IResult Paged(
        NeighborhoodStore store,
        ListRequest request,
        IReadOnlyList<Neighborhood> matches,
        CacheOptions cache,
        string keyPrefix) =>
        new DataResult<PagedResult<Neighborhood>>(
            PagedResult<Neighborhood>.Create(matches, request.Page),
            OpenDataJson.TypeInfo<PagedResult<Neighborhood>>(),
            request.Fields,
            store.Version,
            $"{keyPrefix}&{request.CanonicalKey}",
            request.IsSearch ? cache.Search : cache.Default,
            itemsProperty: "items");
}
