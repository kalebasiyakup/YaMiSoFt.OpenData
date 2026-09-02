namespace YaMiSoFt.OpenData.Core.Querying;

/// <summary>
/// Validated pagination window (FR-03).
/// </summary>
public readonly record struct PageRequest
{
    /// <summary>Largest page size the API will serve; larger requests are rejected.</summary>
    public const int MaxPageSize = 500;

    /// <summary>Page size used when the caller does not specify one.</summary>
    public const int DefaultPageSize = 50;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>One-based page number.</summary>
    public int Page { get; }

    /// <summary>Items per page.</summary>
    public int PageSize { get; }

    /// <summary>Zero-based index of the first item on this page.</summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Validates raw query values. Returns false and sets <paramref name="error"/> when the
    /// caller sent something out of range, so the endpoint can answer with ProblemDetails.
    /// </summary>
    public static bool TryCreate(int? page, int? pageSize, out PageRequest request, out string? error)
    {
        var resolvedPage = page ?? 1;
        var resolvedSize = pageSize ?? DefaultPageSize;

        if (resolvedPage < 1)
        {
            request = default;
            error = "'page' must be 1 or greater.";
            return false;
        }

        if (resolvedSize < 1)
        {
            request = default;
            error = "'pageSize' must be 1 or greater.";
            return false;
        }

        if (resolvedSize > MaxPageSize)
        {
            request = default;
            error = $"'pageSize' must not exceed {MaxPageSize}.";
            return false;
        }

        request = new PageRequest(resolvedPage, resolvedSize);
        error = null;
        return true;
    }
}
