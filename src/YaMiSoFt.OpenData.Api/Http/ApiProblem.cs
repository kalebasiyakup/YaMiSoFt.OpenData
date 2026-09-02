using Microsoft.AspNetCore.Mvc;

namespace YaMiSoFt.OpenData.Api.Http;

/// <summary>
/// Builds the RFC 9457 problem documents the API returns (FR-09).
/// </summary>
/// <remarks>
/// The <c>type</c> URIs are the stable part of an error contract — a client branches on them,
/// not on the human-readable title — so they are minted here in one place rather than spelled
/// out at each call site where a typo would go unnoticed.
/// </remarks>
public static class ApiProblem
{
    private const string TypeBase = "https://opendata.dev/problems/";

    /// <summary>400 for a malformed or out-of-range query parameter.</summary>
    public static IResult InvalidQuery(HttpContext context, string detail) =>
        Create(context, StatusCodes.Status400BadRequest, "invalid-query", "Invalid query parameter", detail);

    /// <summary>404 for a resource identifier that resolves to nothing.</summary>
    public static IResult NotFound(HttpContext context, string type, string title, string detail) =>
        Create(context, StatusCodes.Status404NotFound, type, title, detail);

    /// <summary>Builds a problem document with an explicit status.</summary>
    public static IResult Create(HttpContext context, int status, string type, string title, string detail)
    {
        ArgumentNullException.ThrowIfNull(context);

        return TypedResults.Problem(new ProblemDetails
        {
            Type = TypeBase + type,
            Title = title,
            Status = status,
            Detail = detail,
            Instance = context.Request.Path,
        });
    }
}
