using YaMiSoFt.OpenData.Core.Querying;

namespace YaMiSoFt.OpenData.Data;

/// <summary>
/// The shape every code-keyed reference dataset exposes.
/// </summary>
/// <typeparam name="T">Record type.</typeparam>
/// <remarks>
/// Currencies, languages and time zones differ only in their records: each is a flat list
/// keyed by a short code, searched the same way and paged the same way. Naming that shape
/// lets one endpoint mapper serve all of them, so adding the next such dataset — and Faz 3
/// brings several — is a registration rather than another copy of the same three handlers.
/// </remarks>
public interface IReferenceStore<T>
{
    /// <summary>Dataset version stamp, used to seed response ETags.</summary>
    string Version { get; }

    /// <summary>Number of records held.</summary>
    int Count { get; }

    /// <summary>Every record, in dataset order.</summary>
    IReadOnlyList<T> All { get; }

    /// <summary>Resolves one record by its code, or null.</summary>
    T? Find(string? code);

    /// <summary>Filters and orders the records (FR-03, FR-05).</summary>
    IReadOnlyList<T> Query(string? search, string? sort, SortDirection direction, Language language);
}
