using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MyPoGoCompanion.Api.Health;

internal static class HealthEndpoints
{
    private const string ReadyTag = "ready";

    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new AppVersion(configuration["APP_VERSION"] is { Length: > 0 } version ? version : "dev"));
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(15));
        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealthAsync)
            .WithName("GetHealth")
            .WithTags("Health")
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable)
            .WithSummary("Whether the API and its database are working, and which version is running.");

        // Process only, no dependencies: for container probes that must not wake the database.
        app.MapGet("/health/live", () => TypedResults.Ok(new { status = "Healthy" }))
            .WithName("GetLiveness")
            .WithTags("Health")
            .ExcludeFromDescription();

        return app;
    }

    internal static async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> GetHealthAsync(
        HealthCheckService healthChecks, AppVersion version, CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains(ReadyTag), cancellationToken);
        var body = HealthResponse.From(report, version.Value);
        return report.Status == HealthStatus.Unhealthy
            ? TypedResults.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable)
            : TypedResults.Ok(body);
    }
}

internal sealed record AppVersion(string Value);
