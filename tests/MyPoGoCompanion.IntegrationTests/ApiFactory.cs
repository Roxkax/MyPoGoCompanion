using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>The real API in memory, pointed at the given database and allowing calls from <see cref="WebOrigin"/>.</summary>
public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string WebOrigin = "https://web.example.test";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("Cors:AllowedOrigins", WebOrigin);
        builder.UseSetting("APP_VERSION", "test-version");
    }
}
