using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>
/// The real API in memory, pointed at the given database and allowing calls from <see cref="WebOrigin"/>. Sign-in is
/// configured with test secrets; <paramref name="google"/> stands in for Google's token endpoint when given.
/// </summary>
public sealed class ApiFactory(string connectionString, IGoogleTokenExchange? google = null, bool signInConfigured = true)
    : WebApplicationFactory<Program>
{
    public const string WebOrigin = "https://web.example.test";
    public const string SuperAdminEmail = "owner@example.test";
    public static readonly string SigningKey = Convert.ToBase64String(Enumerable.Range(100, 32).Select(i => (byte)i).ToArray());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("Cors:AllowedOrigins", WebOrigin);
        builder.UseSetting("APP_VERSION", "test-version");
        if (signInConfigured)
        {
            builder.UseSetting("Auth:Google:ClientId", "test-client.apps.googleusercontent.com");
            builder.UseSetting("Auth:Google:ClientSecret", "test-secret");
            builder.UseSetting("Auth:SigningKey", SigningKey);
            builder.UseSetting("Auth:SuperAdminEmail", SuperAdminEmail);
        }

        if (google is not null)
        {
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(google)));
        }
    }
}

/// <summary>Stands in for Google: each code maps to the account it signs in.</summary>
public sealed class FakeGoogle : IGoogleTokenExchange
{
    private readonly Dictionary<string, GoogleIdentity> _accounts = [];

    public FakeGoogle Add(string code, string email, string name)
    {
        _accounts[code] = new GoogleIdentity("google-" + email, email, name, $"https://lh3.googleusercontent.com/a/{name}");
        return this;
    }

    public Task<GoogleIdentity?> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.GetValueOrDefault(code));
}
