using System.Collections.Frozen;

namespace YaMiSoFt.OpenData.Data;

/// <summary>Builds the frozen key indexes the stores resolve lookups against.</summary>
public static class DataIndex
{
    /// <summary>
    /// Indexes <paramref name="items"/> by <paramref name="keySelector"/>, rejecting duplicates.
    /// </summary>
    /// <remarks>
    /// Duplicates fail at startup rather than silently serving one of two rows. The data tests
    /// catch this in CI, but a hand-edited data file must not be able to boot into a state
    /// where a lookup quietly returns the wrong record.
    /// </remarks>
    /// <exception cref="InvalidDataException">Two items share a key.</exception>
    public static FrozenDictionary<TKey, TItem> Build<TItem, TKey>(
        IEnumerable<TItem> items,
        Func<TItem, TKey> keySelector,
        string indexName,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keySelector);

        comparer ??= typeof(TKey) == typeof(string)
            ? (IEqualityComparer<TKey>)StringComparer.OrdinalIgnoreCase
            : EqualityComparer<TKey>.Default;

        var grouped = items.GroupBy(keySelector, comparer).ToArray();

        var duplicate = grouped.FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Duplicate {indexName} key '{duplicate.Key}' in the dataset.");
        }

        return grouped.ToFrozenDictionary(
            static group => group.Key,
            static group => group.Single(),
            comparer);
    }
}
