using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyPoGoCompanion.Api.Health;

namespace MyPoGoCompanion.Api.UnitTests;

public class HealthEndpointsTests
{
    [Fact]
    public async Task GetHealth_Healthy_Returns200WithVersionAndChecks()
    {
        var result = await HealthEndpoints.GetHealthAsync(
            new FixedHealthCheckService(HealthStatus.Healthy), new AppVersion("abc1234"), TestContext.Current.CancellationToken);

        var ok = Assert.IsType<Ok<HealthResponse>>(result.Result);
        Assert.Equal("Healthy", ok.Value!.Status);
        Assert.Equal("abc1234", ok.Value.Version);
        Assert.Equal("Healthy", ok.Value.Checks["database"]);
    }

    [Fact]
    public async Task GetHealth_Unhealthy_Returns503WithoutErrorDetails()
    {
        var result = await HealthEndpoints.GetHealthAsync(
            new FixedHealthCheckService(HealthStatus.Unhealthy), new AppVersion("dev"), TestContext.Current.CancellationToken);

        var json = Assert.IsType<JsonHttpResult<HealthResponse>>(result.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, json.StatusCode);
        Assert.Equal("Unhealthy", json.Value!.Status);
        Assert.Equal(new Dictionary<string, string> { ["database"] = "Unhealthy" }, json.Value.Checks);
    }

    [Fact]
    public async Task GetHealth_Degraded_Returns200()
    {
        var result = await HealthEndpoints.GetHealthAsync(
            new FixedHealthCheckService(HealthStatus.Degraded), new AppVersion("dev"), TestContext.Current.CancellationToken);

        Assert.Equal("Degraded", Assert.IsType<Ok<HealthResponse>>(result.Result).Value!.Status);
    }

    private sealed class FixedHealthCheckService(HealthStatus status) : HealthCheckService
    {
        public override Task<HealthReport> CheckHealthAsync(
            Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default)
        {
            var entry = new HealthReportEntry(status, "secret detail", TimeSpan.Zero, new InvalidOperationException("boom"), null);
            return Task.FromResult(new HealthReport(new Dictionary<string, HealthReportEntry> { ["database"] = entry }, TimeSpan.Zero));
        }
    }
}
