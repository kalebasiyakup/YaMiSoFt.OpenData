using System.Collections.Frozen;
using YaMiSoFt.OpenData.Api.Configuration;
using YaMiSoFt.OpenData.Api.Http;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Core.Querying;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api.Endpoints;

/// <summary>
/// The list, bulk and single-record handler bodies shared by every code-keyed dataset.
/// </summary>
/// <remarks>
/// Currencies, languages and time zones differ only in their records, so the logic lives here
/// once and each endpoint contributes a thin lambda that calls in. That keeps the contract
/// uniform by construction — same validation, same problem shapes, same cache behaviour — and
/// makes the next such dataset a few lines rather than another copy of three handlers.
///
/// The route lambdas themselves stay non-generic on purpose. Mapping a generic lambda crashes
/// <c>RouteHandlerAnalyzer</c> with a null reference, and the only ways around that are to
/// suppress every analyzer crash in the project or to drop to <c>RequestDelegate</c> and lose
/// the OpenAPI parameter metadata that FR-08 depends on. Keeping the generics one call deeper
/// costs nothing and avoids both.
/// </remarks>
public static class ReferenceHandlers
{
    /// <summary>Handles a paged list request.</summary>
    public static IResult List<T>(
        HttpContext context,
        IReferenceStore<T> store,
        CacheOptions cache,
        ListQuery query,
        FrozenSet<string> selectableFields,
        string route)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(cache);

        if (!ListRequest.TryParse(context, query, selectableFields, out var request, out var error))
        {
            return error!;
        }

        var matches = store.Query(request.Search, request.Sort, request.Direction, request.Language);

        return new DataResult<PagedResult<T>>(
            PagedResult<T>.Create(matches, request.Page),
            OpenDataJson.TypeInfo<PagedResult<T>>(),
            request.Fields,
            store.Version,
            $"{route}&{request.CanonicalKey}",
            request.IsSearch ? cache.Search : cache.Default,
            itemsProperty: "items");
    }

    /// <summary>Handles a bulk download request (FR-07).</summary>
    public static IResult All<T>(
        HttpContext context,
        IReferenceStore<T> store,
        CacheOptions cache,
        ListQuery query,
        FrozenSet<string> selectableFields,
        string route)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(cache);

        if (!ListRequest.TryParse(context, query, selectableFields, out var request, out var error))
        {
            return error!;
        }

        return new DataResult<IReadOnlyList<T>>(
            store.All,
            OpenDataJson.TypeInfo<IReadOnlyList<T>>(),
            request.Fields,
            store.Version,
            $"{route}-all&fields={request.Fields.CanonicalKey}",
            cache.Default);
    }

    /// <summary>Handles a single-record lookup by code.</summary>
    public static IResult Single<T>(
        HttpContext context,
        IReferenceStore<T> store,
        CacheOptions cache,
        ListQuery query,
        FrozenSet<string> selectableFields,
        string code,
        string name,
        string codeHint)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(cache);

        if (!ListRequest.TryParse(context, query, selectableFields, out var request, out var error))
        {
            return error!;
        }

        var record = store.Find(code);

        if (record is null)
        {
            var title = char.ToUpperInvariant(name[0]) + name[1..];

            return ApiProblem.NotFound(
                context,
                $"{name}-not-found",
                $"{title} not found",
                $"No {name} matches the code '{EchoedInput.Clip(code)}'. {codeHint}");
        }

        return new DataResult<T>(
            record,
            OpenDataJson.TypeInfo<T>(),
            request.Fields,
            store.Version,
            $"{name}={code.ToLowerInvariant()}&fields={request.Fields.CanonicalKey}",
            cache.Default);
    }
}
