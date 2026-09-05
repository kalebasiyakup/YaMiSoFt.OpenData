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
