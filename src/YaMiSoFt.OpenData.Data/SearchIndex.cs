using System.Collections.Immutable;
using YaMiSoFt.OpenData.Core.Search;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// A collection paired with precomputed, diacritic-folded search keys (FR-05).
/// </summary>
/// <typeparam name="T">Record type.</typeparam>
/// <remarks>
/// Normalization allocates and walks the string twice, so it runs once per record at startup
/// rather than once per record per request; a query only pays to normalize its own term.
/// Every reference dataset needs the same treatment, so the mechanics live here instead of
/// being restated in each store.
/// </remarks>
public sealed class SearchIndex<T>
{
    private readonly ImmutableArray<Entry> _entries;

    /// <summary>
    /// Builds the index.
    /// </summary>
    /// <param name="items">Records to index.</param>
    /// <param name="searchTextSelector">
    /// Returns the values a search term should match against for a record. Nulls are skipped.
    /// </param>
    public SearchIndex(IEnumerable<T> items, Func<T, IEnumerable<string?>> searchTextSelector)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(searchTextSelector);

        _entries =
        [
            .. items.Select(item => new Entry(
                item,
                [
                    .. searchTextSelector(item)
                        .Where(static text => !string.IsNullOrWhiteSpace(text))
                        .Select(SearchTextNormalizer.Normalize)
                        .Where(static key => key.Length > 0)
                        .Distinct(StringComparer.Ordinal),
                ])),
        ];

        Items = [.. _entries.Select(static entry => entry.Item)];
    }

    /// <summary>Every indexed record, in construction order.</summary>
    public ImmutableArray<T> Items { get; }

    /// <summary>Number of indexed records.</summary>
    public int Count => _entries.Length;

    /// <summary>
    /// Returns the records matching <paramref name="rawTerm"/>, or all of them when the term
    /// is blank. The term is normalized once, then compared ordinally against the stored keys.
    /// </summary>
    public IEnumerable<T> Filter(string? rawTerm)
    {
        var term = SearchTextNormalizer.Normalize(rawTerm);

        if (term.Length == 0)
        {
            return Items;
        }

        return _entries
            .Where(entry => entry.Matches(term))
            .Select(static entry => entry.Item);
    }

    private sealed class Entry(T item, ImmutableArray<string> keys)
    {
        internal T Item { get; } = item;

        internal bool Matches(string normalizedTerm)
        {
            foreach (var key in keys)
            {
                if (key.Contains(normalizedTerm, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
