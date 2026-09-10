using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace YaMiSoFt.OpenData.Api.OpenApi;

/// <summary>A sidebar section name and the operation tags it should nest.</summary>
public sealed record TagGroup(string Name, params string[] Tags);

/// <summary>
/// Adds Redocly's <c>x-tagGroups</c> extension to the document root so Scalar's sidebar can
/// nest related tags under one heading instead of listing every tag flat, and so the order of
/// the sections is ours rather than whatever order the endpoints happen to be mapped in.
/// </summary>
/// <remarks>
/// Every tag is placed in exactly one group, even when that means a group of one. Whether
/// Scalar falls back to showing a tag that appears in no group at all is undocumented, and a
/// vanished endpoint in the sidebar is a worse failure than a redundant single-tag group, so
/// this does not rely on that behaviour.
/// </remarks>
/// <param name="groups">Sidebar sections to emit, in display order.</param>
/// <param name="tagNameTranslation">
/// When set (the "v1-tr" document only), also renames the document's own <c>tags</c>
/// declarations to match — <see cref="TurkishTagTransformer"/> already renamed every
/// operation's tag references, and leaving <c>document.Tags</c> in English would mean the
/// <c>x-tagGroups</c> entries above (which name the Turkish tags) reference tags that, as far
/// as the document's own tag list is concerned, do not exist.
/// </param>
public sealed class TagGroupsDocumentTransformer(
    IReadOnlyList<TagGroup> groups,
    IReadOnlyDictionary<string, string>? tagNameTranslation = null) : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (tagNameTranslation is not null && document.Tags is not null)
        {
            foreach (var tag in document.Tags)
            {
                if (tag.Name is { } name && tagNameTranslation.TryGetValue(name, out var turkish))
                {
                    tag.Name = turkish;
                }
            }
        }

        var array = new JsonArray();

        foreach (var group in groups)
        {
            var tags = new JsonArray();
            foreach (var tag in group.Tags)
            {
                tags.Add(JsonValue.Create(tag));
            }

            array.Add(new JsonObject
            {
                ["name"] = group.Name,
                ["tags"] = tags,
            });
        }

        document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        document.Extensions["x-tagGroups"] = new JsonNodeExtension(array);

        return Task.CompletedTask;
    }
}
