using Microsoft.Extensions.Diagnostics.HealthChecks;
using YaMiSoFt.OpenData.Data;

namespace YaMiSoFt.OpenData.Api;

/// <summary>
/// Readiness probe asserting every dataset is loaded and non-empty (NFR-19).
/// </summary>
/// <remarks>
/// Liveness and readiness are deliberately different here. The process is alive as soon as it
/// is listening, but it is only ready once the in-memory datasets exist — otherwise a pod
/// would be handed traffic it can only answer with errors. Reporting the counts makes an
/// incomplete deployment obvious from the probe output alone.
/// </remarks>
public sealed class DataSetHealthCheck(
    TurkeyStore turkey,
    QuarterStore quarters,
    CurrencyStore currencies,
    LanguageStore languages,
    CountryStore countries,
    HolidayStore holidays,
    NeighborhoodStore neighborhoods) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var counts = new (string Name, int Count)[]
        {
            ("provinces", turkey.ProvinceCount),
            ("districts", turkey.DistrictCount),
            ("quarters", quarters.Count),
            ("currencies", currencies.Count),
            ("languages", languages.Count),
            ("countries", countries.Count),
            ("holidays", holidays.Count),
        };

        var data = counts.ToDictionary(
            static entry => entry.Name,
            static entry => (object)entry.Count,
            StringComparer.Ordinal);

        data["version"] = turkey.Version;

        // Reported, never required: settlements load on first use, so a pod is ready before
        // they exist and asserting on them here would keep it out of rotation for no reason.
        data["neighborhoodsLoaded"] = neighborhoods.IsLoaded;

        var empty = counts.Where(static entry => entry.Count == 0).Select(static entry => entry.Name).ToArray();

        if (empty.Length > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Dataset(s) loaded but empty: {string.Join(", ", empty)}.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Datasets loaded.", data));
    }
}
