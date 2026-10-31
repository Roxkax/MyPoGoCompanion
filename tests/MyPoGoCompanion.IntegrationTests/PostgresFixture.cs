using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(MyPoGoCompanion.IntegrationTests.PostgresFixture))]

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>
/// One real PostgreSQL 17 (Neon's version) in Docker, shared by every test in the run. The EF Core migrations
/// are applied once per run to a template database; every database handed out is a copy of it, so it starts
/// with the full schema and nothing else. If the EF Core model changed without a migration, migrating fails and
/// so does every test, with EF's "has pending changes" error.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "migrated_template";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    // PostgreSQL refuses to copy a template while another copy of it is being made.
    private readonly SemaphoreSlim _copying = new(1, 1);
    private int _databases;

    /// <summary>A migrated database shared by the whole run. Tests that write data use <see cref="CreateDatabaseAsync"/>.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await ExecuteAsync($"create database {TemplateDatabase}");

        // Without pooling, no connection stays open on the template once it is migrated (copying needs that).
        await using (var context = CreateContext(ConnectionStringFor(TemplateDatabase, pooling: false)))
        {
            await context.Database.MigrateAsync();
        }

        ConnectionString = await CreateDatabaseAsync();
    }

    /// <summary>A new database of its own with every migration applied, for a test that changes data.</summary>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var name = $"test_{Interlocked.Increment(ref _databases)}";
        await _copying.WaitAsync(cancellationToken);
        try
        {
            await ExecuteAsync($"create database {name} template {TemplateDatabase}", cancellationToken);
        }
        finally
        {
            _copying.Release();
        }

        return ConnectionStringFor(name);
    }

    public static AppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options);

    public async ValueTask DisposeAsync()
    {
        _copying.Dispose();
        await _container.DisposeAsync();
    }

    private string ConnectionStringFor(string database, bool pooling = true) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database, Pooling = pooling }.ConnectionString;

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(ConnectionStringFor("postgres", pooling: false));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
