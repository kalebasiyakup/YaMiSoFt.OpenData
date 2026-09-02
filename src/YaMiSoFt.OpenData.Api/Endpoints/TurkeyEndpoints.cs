using System.Collections.Frozen;
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

/// <summary>Turkish province and district endpoints (BRD 5.2).</summary>
public static class TurkeyEndpoints
{
    private static readonly FrozenSet<string> ProvinceFields = new[]
    {
        "id", "platecode", "name", "nameupper", "slug", "districtcount",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DistrictFields = new[]
    {
        "id", "name", "nameupper", "slug", "provinceid", "provincename", "quartercount",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps the province and district routes onto <paramref name="group"/>.</summary>
    public static IEndpointRouteBuilder MapTurkeyEndpoints(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var provinces = group.MapGroup("/provinces").WithTags("Turkey");

        provinces.MapGet("/", GetProvinces)
            .WithName("ListProvinces")
            .WithBilingualSummary(
                "Lists Turkish provinces (il). Sort by name, id or districts.",
                "Türkiye illerini listeler. İsme, kimliğe veya ilçe sayısına göre sıralanır.");

        provinces.MapGet("/all", GetAllProvinces)
            .WithName("DownloadProvinces")
            .WithBilingualSummary(
                "Downloads all 81 provinces in a single response.",
                "81 ilin tamamını tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        provinces.MapGet("/{idOrSlug}", GetProvince)
            .WithName("GetProvince")
            .WithBilingualSummary(
                "Gets one province by plate code (34) or slug (istanbul).",
                "Plaka koduna (34) veya slug'a (istanbul) göre bir ili getirir.");

        provinces.MapGet("/{idOrSlug}/districts", GetProvinceDistricts)
            .WithName("ListProvinceDistricts")
            .WithBilingualSummary(
                "Lists the districts of one province.",
                "Bir ilin ilçelerini listeler.");

        var districts = group.MapGroup("/districts").WithTags("Turkey");

        districts.MapGet("/", GetDistricts)
            .WithName("ListDistricts")
            .WithBilingualSummary(
                "Lists Turkish districts (ilçe) across all provinces. Sort by name, id, province or quarters.",
                "Tüm illerdeki Türkiye ilçelerini listeler. İsme, kimliğe, ile veya semt sayısına göre sıralanır.");

        districts.MapGet("/all", GetAllDistricts)
            .WithName("DownloadDistricts")
            .WithBilingualSummary(
                "Downloads every district in a single response.",
                "Tüm ilçeleri tek yanıtta indirir.")
            .WithMetadata(new BulkDownloadAttribute());

        districts.MapGet("/{id:int}", GetDistrict)
            .WithName("GetDistrict")
            .WithBilingualSummary(
                "Gets one district by identifier.",
                "Kimliğine göre bir ilçeyi getirir.");

        return group;
    }

    private static IResult GetProvinces(
        HttpContext context,
        TurkeyStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, ProvinceFields, out var request, out var error))
        {
            return error!;
        }

        var matches = store.QueryProvinces(request.Search, request.Sort, request.Direction, request.Language);

        return Paged(store.Version, request, matches, options.Value.Cache);
    }

    private static IResult GetAllProvinces(
        HttpContext context,
        TurkeyStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, ProvinceFields, out var request, out var error))
        {
            return error!;
        }

        return new DataResult<IReadOnlyList<Province>>(
            store.AllProvinces,
            OpenDataJson.TypeInfo<IReadOnlyList<Province>>(),
            request.Fields,
            store.Version,
            $"provinces-all&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    private static IResult GetProvince(
        HttpContext context,
        TurkeyStore store,
        IOptions<OpenDataOptions> options,
        string idOrSlug,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, ProvinceFields, out var request, out var error))
        {
            return error!;
        }

        var province = store.FindProvince(idOrSlug);

        if (province is null)
        {
            return ApiProblem.NotFound(context, "province-not-found", "Province not found",
                $"No province matches '{idOrSlug}'. Use a plate code (1-81) or a slug such as 'istanbul'.");
        }

        return new DataResult<Province>(
            province,
            OpenDataJson.TypeInfo<Province>(),
            request.Fields,
            store.Version,
            $"province={province.Id}&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    /// <summary>
    /// Lists one province's districts (FR-06). The province is resolved first so that an
    /// unknown province is a 404 rather than an empty list — an empty list would read as
    /// "this province has no districts", which is never true.
    /// </summary>
    private static IResult GetProvinceDistricts(
        HttpContext context,
        TurkeyStore store,
        IOptions<OpenDataOptions> options,
        string idOrSlug,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, DistrictFields, out var request, out var error))
        {
            return error!;
        }

        var province = store.FindProvince(idOrSlug);

        if (province is null)
        {
            return ApiProblem.NotFound(context, "province-not-found", "Province not found",
                $"No province matches '{idOrSlug}'. Use a plate code (1-81) or a slug such as 'istanbul'.");
        }

        var matches = request.IsSearch || request.Sort is not null
            ? store.QueryDistricts(request.Search, request.Sort, request.Direction, request.Language, province.Id)
            : store.DistrictsOf(province.Id);

        var result = PagedResult<District>.Create(matches, request.Page);

        return new DataResult<PagedResult<District>>(
            result,
            OpenDataJson.TypeInfo<PagedResult<District>>(),
            request.Fields,
            store.Version,
            $"province={province.Id}&districts&{request.CanonicalKey}",
            request.IsSearch ? options.Value.Cache.Search : options.Value.Cache.Default,
            itemsProperty: "items");
    }

    private static IResult GetDistricts(
        HttpContext context,
        TurkeyStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, DistrictFields, out var request, out var error))
        {
            return error!;
        }

        var matches = store.QueryDistricts(request.Search, request.Sort, request.Direction, request.Language);
        var result = PagedResult<District>.Create(matches, request.Page);

        return new DataResult<PagedResult<District>>(
            result,
            OpenDataJson.TypeInfo<PagedResult<District>>(),
            request.Fields,
            store.Version,
            $"districts&{request.CanonicalKey}",
            request.IsSearch ? options.Value.Cache.Search : options.Value.Cache.Default,
            itemsProperty: "items");
    }

    private static IResult GetAllDistricts(
        HttpContext context,
        TurkeyStore store,
        IOptions<OpenDataOptions> options,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, DistrictFields, out var request, out var error))
        {
            return error!;
        }

        return new DataResult<IReadOnlyList<District>>(
            store.AllDistricts,
            OpenDataJson.TypeInfo<IReadOnlyList<District>>(),
            request.Fields,
            store.Version,
            $"districts-all&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    private static IResult GetDistrict(
        HttpContext context,
        TurkeyStore store,
        IOptions<OpenDataOptions> options,
        int id,
        [AsParameters] ListQuery query)
    {
        if (!ListRequest.TryParse(context, query, DistrictFields, out var request, out var error))
        {
            return error!;
        }

        var district = store.FindDistrict(id);

        if (district is null)
        {
            return ApiProblem.NotFound(context, "district-not-found", "District not found",
                $"No district matches the identifier '{id}'.");
        }

        return new DataResult<District>(
            district,
            OpenDataJson.TypeInfo<District>(),
            request.Fields,
            store.Version,
            $"district={district.Id}&fields={request.Fields.CanonicalKey}",
            options.Value.Cache.Default);
    }

    private static IResult Paged(
        string version,
        ListRequest request,
        IReadOnlyList<Province> matches,
        CacheOptions cache)
    {
        var result = PagedResult<Province>.Create(matches, request.Page);

        return new DataResult<PagedResult<Province>>(
            result,
            OpenDataJson.TypeInfo<PagedResult<Province>>(),
            request.Fields,
            version,
            $"provinces&{request.CanonicalKey}",
            request.IsSearch ? cache.Search : cache.Default,
            itemsProperty: "items");
    }
}
