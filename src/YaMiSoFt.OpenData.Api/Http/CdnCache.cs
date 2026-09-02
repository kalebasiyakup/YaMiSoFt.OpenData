using System.Globalization;

namespace YaMiSoFt.OpenData.Api.Http;

/// <summary>
/// Adds the edge-specific cache directives that let the CDN absorb traffic (NFR-08).
/// </summary>
/// <remarks>
/// This is the single biggest lever on running cost. Every response the edge serves is one the
/// container never wakes up for — and on a scale-to-zero platform an unwoken container is
/// billed nothing at all.
///
/// The edge is told to hold content far longer than browsers are, using separate headers.
/// That combination means a data correction reaches users on the next deploy (which purges the
/// edge) rather than being pinned in a million browser caches for a year, while the edge still
/// answers almost everything without touching the origin.
/// </remarks>
public static class CdnCache
{
    /// <summary>How long the edge may serve a cached response without revalidating.</summary>
    private static readonly TimeSpan EdgeLifetime = TimeSpan.FromDays(365);

    /// <summary>
    /// Applies the edge cache directives alongside an already-set <c>Cache-Control</c>.
    /// </summary>
    /// <param name="response">Response to annotate.</param>
    /// <param name="browserLifetime">
    /// The lifetime already advertised to browsers; the edge lifetime is never shorter.
    /// </param>
    public static void Apply(HttpResponse response, TimeSpan browserLifetime)
    {
        ArgumentNullException.ThrowIfNull(response);

        var edgeSeconds = (long)Math.Max(browserLifetime.TotalSeconds, EdgeLifetime.TotalSeconds);
        var value = string.Create(
            CultureInfo.InvariantCulture,
            $"public, max-age={edgeSeconds}, stale-while-revalidate=86400");

        // CDN-Cache-Control is the cross-vendor header; the Vercel-prefixed one wins on Vercel
        // and is set explicitly so the intent survives a change of provider in either direction.
        response.Headers["CDN-Cache-Control"] = value;
        response.Headers["Vercel-CDN-Cache-Control"] = value;
    }
}
