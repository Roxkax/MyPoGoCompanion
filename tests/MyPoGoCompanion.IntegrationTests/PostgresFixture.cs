using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(MyPoGoCompanion.IntegrationTests.PostgresFixture))]

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>One real PostgreSQL 17 (Neon's version) in Docker, shared by every test in the run.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
