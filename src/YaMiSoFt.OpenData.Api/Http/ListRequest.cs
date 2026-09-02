using System.Collections.Frozen;
using Microsoft.AspNetCore.Mvc;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Api.Http;

/// <summary>
/// The parsed, validated form of the query parameters every list endpoint accepts (FR-03..FR-05).
/// </summary>
/// <remarks>
/// Each dataset endpoint would otherwise repeat the same twenty lines of parsing and the same
/// two error shapes. Centralizing it means a caller gets identical validation behaviour on
/// <c>/countries</c>, <c>/provinces</c> and <c>/currencies</c>, which is a contract in its own
/// right — client code that handles a 400 from one endpoint handles it from all of them.
/// </remarks>
public sealed record ListRequest
{
    private ListRequest(
        PageRequest page,
        FieldSelection fields,
        Language language,
        SortDirection direction,
        string? search,
        string? sort)
    {
        Page = page;
        Fields = fields;
        Language = language;
        Direction = direction;
        Search = search;
        Sort = sort;
    }

    /// <summary>Validated pagination window.</summary>
    public PageRequest Page { get; }

    /// <summary>Canonicalized field selection.</summary>
    public FieldSelection Fields { get; }

    /// <summary>Resolved response language.</summary>
    public Language Language { get; }

    /// <summary>Resolved sort direction.</summary>
    public SortDirection Direction { get; }

    /// <summary>Raw search term, if any.</summary>
    public string? Search { get; }

    /// <summary>Raw sort field, if any.</summary>
    public string? Sort { get; }

    /// <summary>True when the caller supplied a search term.</summary>
    public bool IsSearch => !string.IsNullOrWhiteSpace(Search);

    /// <summary>Stable cache-key fragment for this request (PLAN.md 3.5).</summary>
    public string CanonicalKey =>
        RequestQuery.CanonicalKey(Language, Search, Sort, Direction, Fields, Page);

    /// <summary>
    /// Parses the shared list parameters. Returns false and sets <paramref name="error"/> to a
    /// ready-to-return problem response when anything is out of range.
    /// </summary>
    public static bool TryParse(
        HttpContext context,
        ListQuery query,
        FrozenSet<string> selectableFields,
        out ListRequest request,
        out IResult? error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);

        if (!PageRequest.TryCreate(query.Page, query.PageSize, out var page, out var pageError))
        {
            request = null!;
            error = ApiProblem.InvalidQuery(context, pageError!);
            return false;
        }

        if (!FieldSelection.TryParse(query.Fields, selectableFields, out var fields, out var fieldError))
        {
            request = null!;
            error = ApiProblem.InvalidQuery(context, fieldError!);
            return false;
        }

        request = new ListRequest(
            page,
            fields,
            RequestQuery.ResolveLanguage(context.Request, query.Lang),
            RequestQuery.ResolveDirection(query.Order),
            query.Search,
            query.Sort);
        error = null;
        return true;
    }
}

/// <summary>
/// Raw list query parameters, bound from the query string.
/// </summary>
/// <param name="Page">One-based page number.</param>
/// <param name="PageSize">Items per page, up to 500.</param>
/// <param name="Search">Diacritic-insensitive search term.</param>
/// <param name="Sort">Sort field; the accepted values differ per endpoint.</param>
/// <param name="Order">"asc" or "desc".</param>
/// <param name="Fields">Comma-separated field selection.</param>
/// <param name="Lang">"tr" or "en"; overrides Accept-Language.</param>
public sealed record ListQuery(
    [FromQuery] int? Page = null,
    [FromQuery] int? PageSize = null,
    [FromQuery] string? Search = null,
    [FromQuery] string? Sort = null,
    [FromQuery] string? Order = null,
    [FromQuery] string? Fields = null,
    [FromQuery] string? Lang = null);
