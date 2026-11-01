using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MyPoGoCompanion.Api.Health;

/// <summary>What <c>/health</c> returns. Check details stay in the logs; only their status is public.</summary>
/// <param name="Status">Healthy, Degraded or Unhealthy.</param>
/// <param name="Version">The build that is running (the Git commit in deployed images, "dev" locally).</param>
/// <param name="Checks">Status of each dependency, e.g. "database".</param>
public sealed record HealthResponse(HealthStatus Status, string Version, IReadOnlyDictionary<string, HealthStatus> Checks)
{
    internal static HealthResponse From(HealthReport report, string version) =>
        new(
            report.Status,
            version,
            report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status, StringComparer.Ordinal));
}
