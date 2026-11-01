using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>The EF Core migrations against a real PostgreSQL, and the fresh databases the fixture hands out.</summary>
public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_SharedDatabase_AreAllApplied()
    {
        await using var context = PostgresFixture.CreateContext(postgres.ConnectionString);

        var applied = await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(applied);
        Assert.Equal(context.Database.GetMigrations(), applied);
    }

    [Fact]
    public async Task Migrations_RevertedToEmptyAndAppliedAgain_Succeed()
    {
        // Checks every migration's Down as well as its Up.
        await using var context = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken));
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(Migration.InitialDatabase, TestContext.Current.CancellationToken);
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));

        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateDatabase_EachCall_GivesASeparateMigratedDatabase()
    {
        var first = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        var second = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(first, "create table written_by_a_test (id int)");

        Assert.NotEqual(first, second);
        Assert.Equal(1L, await ExecuteScalarAsync(first, "select count(*) from pg_tables where tablename = 'written_by_a_test'"));
        Assert.Equal(0L, await ExecuteScalarAsync(second, "select count(*) from pg_tables where tablename = 'written_by_a_test'"));
        await using var context = PostgresFixture.CreateContext(second);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Migrations_OnADatabaseMigratedBeforeSchemaV1_ReadItsHistoryAndContinue()
    {
        // QA and prod got the Baseline migration before snake_case names arrived: EF's history table there has
        // its original columns ("MigrationId", "ProductVersion"), and later migrations must keep reading it.
        var connectionString = await postgres.CreateEmptyDatabaseAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connectionString, """
            create table "__EFMigrationsHistory" (
                "MigrationId" character varying(150) not null constraint "PK___EFMigrationsHistory" primary key,
                "ProductVersion" character varying(32) not null);
            insert into "__EFMigrationsHistory" values ('20261031004347_Baseline', '10.0.14');
            """);
        await using var context = PostgresFixture.CreateContext(connectionString);

        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(context.Database.GetMigrations().Count(),
            Convert.ToInt32(await ExecuteScalarAsync(connectionString, """select count(*) from "__EFMigrationsHistory" """), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0L, await ExecuteScalarAsync(connectionString,
            "select count(*) from information_schema.columns where table_name = '__EFMigrationsHistory' and column_name = 'migration_id'"));
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(string connectionString, string sql)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var command = dataSource.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
