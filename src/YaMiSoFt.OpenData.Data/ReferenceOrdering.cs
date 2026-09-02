using System.Globalization;
using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// Culture-aware ordering shared by the list endpoints (FR-03, FR-04).
/// </summary>
/// <remarks>
/// Turkish collation is not the invariant one: it orders c-cedilla, g-breve, i-dotless,
/// s-cedilla and the umlauts as distinct letters after their bare counterparts, so
/// "Canakkale" and "Cankiri" land where a Turkish reader expects only under tr-TR. Sorting a
/// province list with the invariant comparer produces an order that looks subtly wrong to
/// every Turkish user, which is the audience this project exists for.
/// </remarks>
public static class ReferenceOrdering
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Returns the string comparer matching <paramref name="language"/>.</summary>
    public static StringComparer ComparerFor(Language language) =>
        StringComparer.Create(
            language == Language.Turkish ? TurkishCulture : EnglishCulture,
            ignoreCase: true);

    /// <summary>Applies a text ordering in the requested direction.</summary>
    public static IOrderedEnumerable<T> ByText<T>(
        IEnumerable<T> source,
        Func<T, string> keySelector,
        SortDirection direction,
        Language language)
    {
        var comparer = ComparerFor(language);

        return direction == SortDirection.Descending
            ? source.OrderByDescending(keySelector, comparer)
            : source.OrderBy(keySelector, comparer);
    }

    /// <summary>Applies a numeric ordering in the requested direction.</summary>
    public static IOrderedEnumerable<T> ByNumber<T, TKey>(
        IEnumerable<T> source,
        Func<T, TKey> keySelector,
        SortDirection direction)
    {
        return direction == SortDirection.Descending
            ? source.OrderByDescending(keySelector)
            : source.OrderBy(keySelector);
    }
}
