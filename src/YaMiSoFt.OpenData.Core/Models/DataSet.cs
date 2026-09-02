namespace YaMiSoFt.OpenData.Core.Models;

/// <summary>
/// Envelope around a committed data file. The provenance fields exist so every dataset
/// carries its own licence trail (BRD 11, "lisans ihlali" risk) and so <see cref="Version"/>
/// can seed response ETags without hashing payloads (PLAN.md 3.6).
/// </summary>
/// <typeparam name="T">Record type held in <see cref="Items"/>.</typeparam>
public sealed record DataSet<T>
{
    /// <summary>Dataset identifier, e.g. "countries".</summary>
    public required string Name { get; init; }

    /// <summary>Version stamp, ISO-8601 date of the last regeneration.</summary>
    public required string Version { get; init; }

    /// <summary>Upstream source the records were derived from.</summary>
    public required string Source { get; init; }

    /// <summary>SPDX licence identifier of the upstream source.</summary>
    public required string License { get; init; }

    /// <summary>The records.</summary>
    public required IReadOnlyList<T> Items { get; init; }
}
