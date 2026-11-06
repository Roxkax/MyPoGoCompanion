using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>
/// The real API in memory, pointed at the given database and allowing calls from <see cref="WebOrigin"/>. Sign-in is
/// configured with test secrets; <paramref name="google"/> stands in for Google's token endpoint when given;
/// <paramref name="settings"/> adds or overrides configuration (e.g. <c>ApiDocs:Enabled</c>); <paramref name="importStarter"/>
/// stands in for Azure when Run now starts the daily import.
/// </summary>
public sealed class ApiFactory(
    string connectionString, IGoogleTokenExchange? google = null, bool signInConfigured = true,
    IReadOnlyDictionary<string, string?>? settings = null, IImportStarter? importStarter = null)
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

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }

        if (google is not null)
        {
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(google)));
        }

        if (importStarter is not null)
        {
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(importStarter)));
        }
    }
}

/// <summary>Stands in for Azure's job start: records each run asked for, or fails when told to.</summary>
public sealed class FakeImportStarter : IImportStarter
{
    public List<ImportRunRequest> Requests { get; } = [];

    /// <summary>When set, the next start fails with this reason (as Azure refusing would).</summary>
    public string? FailWith { get; set; }

    public bool IsConfigured => true;

    public Task<string> StartAsync(ImportRunRequest request, CancellationToken cancellationToken)
    {
        if (FailWith is { } reason)
        {
            FailWith = null;
            throw new JobStartException(reason);
        }

        Requests.Add(request);
        return Task.FromResult($"job-import-test-{Requests.Count}");
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
