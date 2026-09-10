using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace YaMiSoFt.OpenData.Api.OpenApi;

/// <summary>
/// Orders the document's own <c>tags</c> declarations so Scalar's sidebar follows a chosen
/// order instead of whatever order the endpoints happened to be mapped in.
/// </summary>
/// <remarks>
/// This used to wrap every tag in Redocly's <c>x-tagGroups</c> extension — one group per tag,
/// since nothing in this API shares a parent topic — to get the same ordering. Looked at in
/// Scalar itself, that rendered a menu entry and a single nested item underneath it with the
/// identical label for every dataset ("Banks" over "Banks", say): a redundant extra level, not
/// a real group. A tag list with no shared parents does not need grouping at all — Scalar
/// reads the document's plain <c>tags</c> order for the flat sidebar, so reordering that array
/// is enough.
/// </remarks>
/// <param name="order">Tag names in the order they should appear in the sidebar.</param>
/// <param name="tagNameTranslation">
/// When set (the "v1-tr" document only), also renames the document's own <c>tags</c>
/// declarations — <see cref="TurkishTagTransformer"/> already renamed every operation's tag
/// references, and leaving <c>document.Tags</c> in English would mean <paramref name="order"/>
/// (which names the Turkish tags) matches nothing in the document's own tag list.
/// </param>
public sealed class TagOrderDocumentTransformer(
    IReadOnlyList<string> order,
    IReadOnlyDictionary<string, string>? tagNameTranslation = null) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Tags is null)
        {
            return Task.CompletedTask;
        }

        if (tagNameTranslation is not null)
        {
            foreach (var tag in document.Tags)
            {
                if (tag.Name is { } name && tagNameTranslation.TryGetValue(name, out var turkish))
                {
                    tag.Name = turkish;
                }
            }
        }

        var rank = order
            .Select(static (name, index) => (name, index))
            .ToDictionary(static entry => entry.name, static entry => entry.index, StringComparer.Ordinal);

        document.Tags = document.Tags
            .OrderBy(tag => tag.Name is { } name && rank.TryGetValue(name, out var index) ? index : int.MaxValue)
            .ToHashSet();

        return Task.CompletedTask;
    }
}
