using System.Net;
using System.Text.Json;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class OpenApiTests(PostgresFixture postgres)
{
    [Fact]
    public async Task OpenApi_Document_DescribesHealth()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("MyPoGoCompanion API", document.RootElement.GetProperty("info").GetProperty("title").GetString());
        var health = document.RootElement.GetProperty("paths").GetProperty("/health").GetProperty("get");
        Assert.Equal("GetHealth", health.GetProperty("operationId").GetString());
        Assert.True(health.GetProperty("responses").TryGetProperty("503", out _));
        Assert.False(document.RootElement.GetProperty("paths").TryGetProperty("/health/live", out _));
    }
}
