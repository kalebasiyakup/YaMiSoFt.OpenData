using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Net.Http.Headers;
using YaMiSoFt.OpenData.Core.Json;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Api.Http;

/// <summary>
/// Writes a dataset response with validation and freshness headers attached (NFR-06, NFR-07).
/// </summary>
/// <typeparam name="T">Payload type.</typeparam>
/// <remarks>
/// The ETag is derived from the dataset version plus the canonical request key rather than
/// from the serialized body. Reference data only changes when a deploy ships a new data file,
/// so the version already identifies the content, and skipping the body hash means a
/// conditional request can be answered with 304 without ever serializing the payload.
/// </remarks>
public sealed class DataResult<T>(
    T value,
    JsonTypeInfo<T> typeInfo,
    FieldSelection fields,
    string dataVersion,
    string canonicalKey,
    TimeSpan cacheDuration,
    string? itemsProperty = null) : IResult
{
    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var etag = BuildETag(dataVersion, canonicalKey);
        var headers = httpContext.Response.GetTypedHeaders();

        headers.ETag = new EntityTagHeaderValue(etag, isWeak: true);
        headers.CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = cacheDuration,
        };

        // Content varies by language and encoding; a shared cache must key on both.
        httpContext.Response.Headers.Vary = "Accept-Encoding, Accept-Language";

        CdnCache.Apply(httpContext.Response, cacheDuration);

        if (IsNotModified(httpContext.Request, etag))
        {
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        httpContext.Response.ContentType = "application/json; charset=utf-8";

        if (fields.IsAll)
        {
            await httpContext.Response
                .WriteAsJsonAsync(value, typeInfo, contentType: null, httpContext.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        // Partial responses go through the node tree so that any payload shape supports
        // ?fields= without a parallel projection type per endpoint (FR-03).
        var node = JsonSerializer.SerializeToNode(value, typeInfo);
        var projected = JsonFieldProjector.Project(node, fields, itemsProperty);

        await httpContext.Response
            .WriteAsync(
                projected?.ToJsonString(OpenDataJson.Options) ?? "null",
                httpContext.RequestAborted)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a deterministic validator. <see cref="string.GetHashCode()"/> is deliberately
    /// avoided: it is salted per process, so two replicas would hand out different ETags for
    /// identical content and defeat both client and CDN caching.
    /// </summary>
    private static string BuildETag(string version, string canonicalKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalKey));
        var suffix = Convert.ToHexString(digest.AsSpan(0, 8)).ToLowerInvariant();

        return string.Create(CultureInfo.InvariantCulture, $"\"{version}-{suffix}\"");
    }

    private static bool IsNotModified(HttpRequest request, string etag)
    {
        var ifNoneMatch = request.Headers.IfNoneMatch;

        if (ifNoneMatch.Count == 0)
        {
            return false;
        }

        foreach (var candidate in ifNoneMatch)
        {
            if (candidate is null)
            {
                continue;
            }

            if (candidate == "*")
            {
                return true;
            }

            // Compare weakly: the weak prefix and surrounding whitespace carry no meaning here.
            foreach (var part in candidate.Split(',', StringSplitOptions.TrimEntries))
            {
                var normalized = part.StartsWith("W/", StringComparison.Ordinal) ? part[2..] : part;

                if (string.Equals(normalized, etag, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
