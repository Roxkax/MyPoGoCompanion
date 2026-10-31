using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyPoGoCompanion.Api.Health;
using Npgsql;

namespace MyPoGoCompanion.Api.UnitTests;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealth_NoConnectionString_IsUnhealthy()
    {
        var check = new DatabaseHealthCheck(new ServiceCollection().BuildServiceProvider());

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("No database connection string is configured.", result.Description);
    }

    [Fact]
    public async Task CheckHealth_DatabaseUnreachable_IsUnhealthyWithTheError()
    {
        // Port 1 on the local machine refuses the connection straight away.
        await using var services = new ServiceCollection()
            .AddNpgsqlDataSource("Host=127.0.0.1;Port=1;Timeout=2")
            .BuildServiceProvider();
        var check = new DatabaseHealthCheck(services);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("The database could not be reached.", result.Description);
        Assert.IsAssignableFrom<NpgsqlException>(result.Exception);
    }
}
