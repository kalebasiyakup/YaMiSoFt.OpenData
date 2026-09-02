using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Api.Http;

/// <summary>Helpers for reading the shared list-endpoint query parameters.</summary>
public static class RequestQuery
{
    /// <summary>
    /// Resolves the response language from <c>?lang=</c>, falling back to
    /// <c>Accept-Language</c>, then to English (FR-04).
    /// </summary>
    public static Language ResolveLanguage(HttpRequest request, string? lang)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(lang))
        {
            return Parse(lang);
        }

        var header = request.Headers.AcceptLanguage.ToString();

        // A full Accept-Language is a weighted list; the first tag is a good enough signal
        // for a two-language API and avoids pulling in a negotiation library.
        var first = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => value.Split(';', 2)[0].Trim())
            .FirstOrDefault();

        return Parse(first);
    }

    /// <summary>Parses the sort direction from <c>?order=</c>. Defaults to ascending.</summary>
    public static SortDirection ResolveDirection(string? order) =>
        string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Descending
            : SortDirection.Ascending;

    /// <summary>
    /// Builds the canonical cache-key fragment for a list request. Order is fixed and values
    /// are normalized so that equivalent requests written differently share one cache entry
    /// and one ETag (PLAN.md 3.5).
    /// </summary>
    public static string CanonicalKey(
        Language language,
        string? search,
        string? sort,
        SortDirection direction,
        FieldSelection fields,
        PageRequest page)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var normalizedSearch = Core.Search.SearchTextNormalizer.Normalize(search);
        var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "name" : sort.ToLowerInvariant();

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"lang={language}&search={normalizedSearch}&sort={normalizedSort}&order={direction}&fields={fields.CanonicalKey}&page={page.Page}&size={page.PageSize}");
    }

    private static Language Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Language.English;
        }

        // Match on the primary subtag so "tr-TR" and "tr" behave the same.
        var primary = value.Split('-', 2)[0];

        return primary.Equals("tr", StringComparison.OrdinalIgnoreCase)
            ? Language.Turkish
            : Language.English;
    }
}
