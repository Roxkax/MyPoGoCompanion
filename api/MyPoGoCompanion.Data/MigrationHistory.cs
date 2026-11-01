using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace MyPoGoCompanion.Data;

/// <summary>
/// EF's migration history table (<c>__EFMigrationsHistory</c>) keeps EF's own column names, "MigrationId" and
/// "ProductVersion". The snake_case naming convention would otherwise rename them, and the databases migrated
/// before it (QA and prod got the Baseline migration with the original names) couldn't be read any more.
/// </summary>
#pragma warning disable EF1001 // Npgsql's history repository lives in an "Internal" namespace; deriving from it is how EF documents customising the table.
internal sealed class MigrationHistory(HistoryRepositoryDependencies dependencies) : NpgsqlHistoryRepository(dependencies)
#pragma warning restore EF1001
{
    public const string MigrationIdColumn = "MigrationId";

    public const string ProductVersionColumn = "ProductVersion";

    protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
    {
        base.ConfigureTable(history);
        history.Property(row => row.MigrationId).HasColumnName(MigrationIdColumn);
        history.Property(row => row.ProductVersion).HasColumnName(ProductVersionColumn);
    }
}
