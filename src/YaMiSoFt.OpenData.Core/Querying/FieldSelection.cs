using System.Collections.Frozen;

namespace YaMiSoFt.OpenData.Core.Querying;

/// <summary>
/// Canonicalized <c>fields=</c> partial-response selector (FR-03).
/// </summary>
/// <remarks>
/// The canonical form matters as much as the selection itself. Cache keys include the raw
/// query string, so "fields=code,name" and "fields=name,code" would otherwise occupy two
/// cache entries for one response. Parsing sorts, lowercases and de-duplicates the list, and
/// <see cref="CanonicalKey"/> is what the output cache varies on (PLAN.md 3.5).
/// </remarks>
public sealed class FieldSelection
{
    private static readonly FieldSelection AllFields = new(FrozenSet<string>.Empty, string.Empty);

    private FieldSelection(FrozenSet<string> fields, string canonicalKey)
    {
        Fields = fields;
        CanonicalKey = canonicalKey;
    }

    /// <summary>Selector that keeps every field.</summary>
    public static FieldSelection All => AllFields;

    /// <summary>Selected field names, or empty when every field is kept.</summary>
    public FrozenSet<string> Fields { get; }

    /// <summary>Stable cache-key fragment: sorted, comma-joined, empty when unrestricted.</summary>
    public string CanonicalKey { get; }

    /// <summary>True when no projection is applied.</summary>
    public bool IsAll => Fields.Count == 0;

    /// <summary>
    /// Parses a raw <c>fields</c> query value against the set the endpoint allows.
    /// Unknown names are rejected rather than ignored, so a typo surfaces as a 400 instead of
    /// silently returning a response missing the field the caller wanted.
    /// </summary>
    public static bool TryParse(
        string? raw,
        FrozenSet<string> allowed,
        out FieldSelection selection,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            selection = AllFields;
            error = null;
            return true;
        }

        var requested = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static field => field.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (requested.Length == 0)
        {
            selection = AllFields;
            error = null;
            return true;
        }

        var unknown = requested.Where(field => !allowed.Contains(field)).ToArray();
        if (unknown.Length > 0)
        {
            selection = AllFields;
            // Only the first few unknown names are echoed: the caller sent them, so the list
            // is theirs to make arbitrarily long, and naming three is enough to find the typo.
            var echoed = unknown.Take(3).Select(static field => field.Length <= 32 ? field : field[..32] + "…");

            error = $"Unknown field(s): {string.Join(", ", echoed)}. Allowed: {string.Join(", ", allowed.Order(StringComparer.Ordinal))}.";
            return false;
        }

        selection = new FieldSelection(
            requested.ToFrozenSet(StringComparer.Ordinal),
            string.Join(',', requested));
        error = null;
        return true;
    }
}
