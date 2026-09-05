using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace YaMiSoFt.OpenData.Api.OpenApi;

/// <summary>
/// Carries an endpoint's Turkish summary alongside the English one that <c>WithSummary</c>
/// already put in <c>OpenApiOperation.Summary</c> (BRD §12: English primary, Turkish ek/supplement).
/// </summary>
internal sealed class BilingualSummaryMetadata(string turkish)
{
    public string Turkish { get; } = turkish;
}

/// <summary>Attaches both language summaries an endpoint needs for the two OpenAPI documents.</summary>
public static class EndpointSummaryExtensions
{
    /// <summary>
    /// Sets the English summary as the endpoint's primary <c>WithSummary</c> text and stashes the
    /// Turkish counterpart as metadata for <see cref="TurkishSummaryTransformer"/> to apply to the
    /// "v1-tr" OpenAPI document.
    /// </summary>
    public static RouteHandlerBuilder WithBilingualSummary(
        this RouteHandlerBuilder builder, string english, string turkish) =>
        builder.WithSummary(english).WithMetadata(new BilingualSummaryMetadata(turkish));
}

/// <summary>
/// Swaps every operation's summary to Turkish when building the "v1-tr" OpenAPI document, using the
/// text <see cref="EndpointSummaryExtensions.WithBilingualSummary"/> attached to the endpoint.
/// </summary>
public sealed class TurkishSummaryTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<BilingualSummaryMetadata>()
            .FirstOrDefault();

        if (metadata is not null)
        {
            operation.Summary = metadata.Turkish;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Renames every operation's tag to Turkish when building the "v1-tr" OpenAPI document. The
/// English tag set by <c>WithTags</c> is the primary/base value (BRD §12); this is the same
/// English-primary-Turkish-supplement split <see cref="TurkishSummaryTransformer"/> applies to
/// summaries, just for tags instead.
/// </summary>
public sealed class TurkishTagTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (operation.Tags is null)
        {
            return Task.CompletedTask;
        }

        operation.Tags = new HashSet<OpenApiTagReference>(operation.Tags.Select(TranslateTag));

        return Task.CompletedTask;
    }

    private static OpenApiTagReference TranslateTag(OpenApiTagReference tag) =>
        tag.Name is { } name && TurkishTagNames.Map.TryGetValue(name, out var turkish)
            ? new OpenApiTagReference(turkish)
            : tag;
}

/// <summary>English tag name (as set by <c>WithTags</c>) to its Turkish counterpart.</summary>
/// <remarks>
/// Shared by <see cref="TurkishTagTransformer"/> (renames each operation's tag references) and
/// <see cref="TagGroupsDocumentTransformer"/> (also renames the document's own <c>tags</c>
/// declarations for the "v1-tr" document) so the two never drift apart into naming a tag two
/// different things in the same document.
/// </remarks>
internal static class TurkishTagNames
{
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Address"] = "Adres",
        ["Mobile Operators"] = "GSM Operatörleri",
        ["Countries"] = "Ülke Kodları",
        ["Currencies"] = "Para Birimleri",
        ["Languages"] = "Diller",
        ["Public Holidays"] = "Resmi Tatiller",
    };
}
