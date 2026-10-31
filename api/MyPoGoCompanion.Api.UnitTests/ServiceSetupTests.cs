using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Configuration;
using MyPoGoCompanion.Api.Health;
using Npgsql;

namespace MyPoGoCompanion.Api.UnitTests;

/// <summary>How the API's services are registered from configuration.</summary>
public class ServiceSetupTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void AddDatabase_WithConnectionString_RegistersTheDataSource()
    {
        using var services = new ServiceCollection()
            .AddDatabase(Config(("ConnectionStrings:Database", "Host=db.example;Database=app;Username=u;Password=p")))
            .BuildServiceProvider();

        var dataSource = services.GetRequiredService<NpgsqlDataSource>();
        Assert.Contains("Host=db.example", dataSource.ConnectionString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void AddDatabase_WithoutConnectionString_RegistersNothing(string? connectionString)
    {
        using var services = new ServiceCollection()
            .AddDatabase(Config(("ConnectionStrings:Database", connectionString)))
            .BuildServiceProvider();

        Assert.Null(services.GetService<NpgsqlDataSource>());
    }

    [Fact]
    public void AddWebAppCors_AllowsTheConfiguredOrigins()
    {
        using var services = new ServiceCollection()
            .AddWebAppCors(Config(("Cors:AllowedOrigins", "https://app.example/,http://localhost:5173")))
            .BuildServiceProvider();

        var options = services.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = options.GetPolicy(options.DefaultPolicyName)!;
        Assert.Equal(["https://app.example", "http://localhost:5173"], policy.Origins);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
    }

    [Theory]
    [InlineData("abc1234", "abc1234")]
    [InlineData("", "dev")]
    [InlineData(null, "dev")]
    public void AddApiHealthChecks_ReportsTheAppVersion(string? configured, string expected)
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddApiHealthChecks(Config(("APP_VERSION", configured)))
            .BuildServiceProvider();

        Assert.Equal(expected, services.GetRequiredService<AppVersion>().Value);
    }

    [Fact]
    public void AddApiHealthChecks_RegistersTheDatabaseCheck()
    {
        using var services = new ServiceCollection()
            .AddLogging()
            .AddApiHealthChecks(Config())
            .BuildServiceProvider();

        var registration = Assert.Single(services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations);
        Assert.Equal("database", registration.Name);
        Assert.Contains("ready", registration.Tags);
    }

    [Fact]
    public async Task MapHealthEndpoints_MapsHealthAndLiveness()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddApiHealthChecks(builder.Configuration);
        await using var app = builder.Build();

        app.MapHealthEndpoints();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);
        Assert.Equal(["/health", "/health/live"], routes);
    }
}
