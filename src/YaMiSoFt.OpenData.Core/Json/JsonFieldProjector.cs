using System.Text.Json.Nodes;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Core.Json;

/// <summary>
/// Applies a <see cref="FieldSelection"/> to already-serialized JSON (FR-03 partial response).
/// </summary>
/// <remarks>
/// Projection happens on the node tree rather than through a per-shape DTO so that any model
/// gains partial-response support without a parallel set of projection types.
/// </remarks>
public static class JsonFieldProjector
{
    /// <summary>
    /// Removes every property not named in <paramref name="selection"/>. Arrays are projected
    /// element-wise. Returns <paramref name="node"/> unchanged when the selection is unrestricted.
    /// </summary>
    /// <param name="node">Serialized payload.</param>
    /// <param name="selection">Fields to keep.</param>
    /// <param name="itemsProperty">
    /// When the payload is an envelope around a collection — a paged result, say — names the
    /// property holding the records. The projection then applies to those records and leaves
    /// the envelope's own metadata alone. Without this, <c>?fields=alpha2</c> on a paged
    /// response would strip <c>items</c>, <c>page</c> and <c>totalCount</c> alike and return
    /// an empty object. Null projects the node itself.
    /// </param>
    public static JsonNode? Project(JsonNode? node, FieldSelection selection, string? itemsProperty = null)
    {
        if (node is null || selection.IsAll)
        {
            return node;
        }

        if (itemsProperty is not null)
        {
            return ProjectEnvelope(node, selection, itemsProperty);
        }

        return node switch
        {
            JsonArray array => ProjectArray(array, selection),
            JsonObject item => ProjectObject(item, selection),
            _ => node,
        };
    }

    private static JsonNode? ProjectEnvelope(JsonNode node, FieldSelection selection, string itemsProperty)
    {
        if (node is not JsonObject envelope || envelope[itemsProperty] is not JsonArray items)
        {
            return node;
        }

        var projected = new JsonObject();

        foreach (var property in envelope)
        {
            projected[property.Key] = property.Key == itemsProperty
                ? ProjectArray(items, selection)
                : property.Value?.DeepClone();
        }

        return projected;
    }

    private static JsonArray ProjectArray(JsonArray array, FieldSelection selection)
    {
        var projected = new JsonArray();

        foreach (var element in array)
        {
            projected.Add(Project(element?.DeepClone(), selection));
        }

        return projected;
    }

    private static JsonObject ProjectObject(JsonObject item, FieldSelection selection)
    {
        var projected = new JsonObject();

        foreach (var property in item)
        {
            if (selection.Fields.Contains(property.Key.ToLowerInvariant()))
            {
                projected[property.Key] = property.Value?.DeepClone();
            }
        }

        return projected;
    }
}
