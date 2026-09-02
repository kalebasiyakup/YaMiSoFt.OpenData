namespace YaMiSoFt.OpenData.Core.Querying;

/// <summary>
/// A page of results plus the metadata clients need to walk the collection (FR-03).
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>Items on the current page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>One-based page number that produced <see cref="Items"/>.</summary>
    public required int Page { get; init; }

    /// <summary>Requested page size.</summary>
    public required int PageSize { get; init; }

    /// <summary>Total matching items across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>Total number of pages at the current page size.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>True when a further page exists.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>True when a previous page exists.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>Projects <paramref name="source"/> onto a page.</summary>
    public static PagedResult<T> Create(IReadOnlyList<T> source, PageRequest page) => new()
    {
        Items = source.Skip(page.Skip).Take(page.PageSize).ToArray(),
        Page = page.Page,
        PageSize = page.PageSize,
        TotalCount = source.Count,
    };
}
