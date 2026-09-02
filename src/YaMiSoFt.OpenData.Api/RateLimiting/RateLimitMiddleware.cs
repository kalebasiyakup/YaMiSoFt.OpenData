using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using YaMiSoFt.OpenData.Api.Configuration;

namespace YaMiSoFt.OpenData.Api.RateLimiting;

/// <summary>
/// Marks an endpoint as billed against the bulk-download allowance (PLAN.md 3.8).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BulkDownloadAttribute : Attribute;

/// <summary>
/// Enforces the tiered request allowances and reports remaining quota on every response
/// (NFR-01, NFR-02, NFR-03).
/// </summary>
/// <remarks>
/// The headers are the reason this is a middleware rather than the framework's rate limiting
/// middleware: NFR-03 wants quota state on <em>every</em> response, including successful ones,
/// which means the counters have to be read on the request path and written to the response.
/// </remarks>
public sealed class RateLimitMiddleware(
    RequestDelegate next,
    IRateLimitStore store,
    ClientIdentityResolver identityResolver,
    IOptions<OpenDataOptions> options,
    TimeProvider timeProvider,
    ILogger<RateLimitMiddleware> logger)
{
    private const string LimitHeader = "X-RateLimit-Limit";
    private const string RemainingHeader = "X-RateLimit-Remaining";
    private const string ResetHeader = "X-RateLimit-Reset";

    private readonly OpenDataOptions _options = options.Value;

    /// <summary>Runs the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.RateLimit.Enabled || IsExempt(context))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var identity = identityResolver.Resolve(context);
        var tier = ResolveTier(context, identity);
        var partitionKey = BuildPartitionKey(context, identity);

        var decision = await store
            .AcquireAsync(partitionKey, tier, context.RequestAborted)
            .ConfigureAwait(false);

        WriteHeaders(context, decision);

        if (decision.Allowed)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        await RejectAsync(context, decision).ConfigureAwait(false);
    }

    /// <summary>
    /// Health and metrics endpoints are never limited: an orchestrator probing readiness must
    /// not be able to exhaust a quota and take the pod out of rotation.
    /// </summary>
    private static bool IsExempt(HttpContext context)
    {
        var path = context.Request.Path;

        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);
    }

    private TierOptions ResolveTier(HttpContext context, ClientIdentity identity)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<BulkDownloadAttribute>() is not null)
        {
            return _options.RateLimit.BulkDownload;
        }

        return identity.Tier == ClientTier.ApiKey
            ? _options.RateLimit.ApiKey
            : _options.RateLimit.Anonymous;
    }

    /// <summary>
    /// Bulk downloads get their own partition so that exhausting the bulk allowance does not
    /// also lock the caller out of ordinary paged reads.
    /// </summary>
    private static string BuildPartitionKey(HttpContext context, ClientIdentity identity) =>
        context.GetEndpoint()?.Metadata.GetMetadata<BulkDownloadAttribute>() is not null
            ? $"bulk|{identity.Key}"
            : identity.Key;

    private static void WriteHeaders(HttpContext context, RateLimitDecision decision)
    {
        var headers = context.Response.Headers;

        headers[LimitHeader] = decision.Limit.ToString(CultureInfo.InvariantCulture);
        headers[RemainingHeader] = decision.Remaining.ToString(CultureInfo.InvariantCulture);
        headers[ResetHeader] = decision.ResetsAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        // Quota is per-caller, so a shared cache must never replay one caller's response
        // (and their remaining count) to another.
        context.Response.Headers.Vary = "Accept-Encoding, Accept-Language";
    }

    private async Task RejectAsync(HttpContext context, RateLimitDecision decision)
    {
        var now = timeProvider.GetUtcNow();
        var retryAfter = decision.RetryAfterSeconds(now);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

        logger.LogInformation(
            "Rate limit reached for {Path}; retry in {RetryAfter}s.",
            context.Request.Path,
            retryAfter);

        var problem = new ProblemDetails
        {
            Type = "https://opendata.dev/problems/rate-limit-exceeded",
            Title = "Too many requests",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = $"Request allowance exhausted. Retry in {retryAfter} seconds. "
                + "Use the /all bulk endpoints and cache locally to reduce request volume.",
            Instance = context.Request.Path,
        };

        await context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted).ConfigureAwait(false);
    }
}
