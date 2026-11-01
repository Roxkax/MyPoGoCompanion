using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyPoGoCompanion.Api.Health;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class HealthTests(PostgresFixture postgres)
{
    /// <summary>The API writes statuses as their names ("Healthy").</summary>
    private static readonly JsonSerializerOptions _json = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Health_RealDatabase_ReturnsHealthyWithVersion()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(_json, TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Healthy, body!.Status);
        Assert.Equal("test-version", body.Version);
        Assert.Equal(HealthStatus.Healthy, body.Checks["database"]);
    }

    [Fact]
    public async Task Health_DatabaseUnreachable_Returns503WithoutDetails()
    {
        // Nothing listens on port 1; the error details must not reach the response.
        await using var api = new ApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=hunter2;Timeout=2");
        using var client = api.CreateClient();

        var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var body = JsonSerializer.Deserialize<HealthResponse>(text, _json);
        Assert.Equal(HealthStatus.Unhealthy, body!.Checks["database"]);
        Assert.Contains("\"database\":\"Unhealthy\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Liveness_DatabaseUnreachable_Returns200()
    {
        await using var api = new ApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=none;Timeout=2");
        using var client = api.CreateClient();

        var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cors_WebAppOrigin_IsAllowed()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/health", UriKind.Relative));
        request.Headers.Add("Origin", ApiFactory.WebOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(ApiFactory.WebOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Cors_OtherOrigin_IsNotAllowed()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/health", UriKind.Relative));
        request.Headers.Add("Origin", "https://evil.example.test");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
