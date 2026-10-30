using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace MyPoGoCompanion.Api.Health;

/// <summary>Opens a connection and runs <c>select 1</c>. Wakes a suspended Neon database, so it can take a few seconds.</summary>
internal sealed class DatabaseHealthCheck(IServiceProvider services) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var dataSource = services.GetService<NpgsqlDataSource>();
        if (dataSource is null)
        {
            return HealthCheckResult.Unhealthy("No database connection string is configured.");
        }

        try
        {
            await using var command = dataSource.CreateCommand("select 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            // The health check service logs the exception; the public response shows only the status.
            return HealthCheckResult.Unhealthy("The database could not be reached.", exception);
        }
    }
}
