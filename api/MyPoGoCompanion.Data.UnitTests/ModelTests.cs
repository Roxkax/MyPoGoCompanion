using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.UnitTests;

/// <summary>The rules the EF Core model follows, checked without a database.</summary>
public partial class ModelTests
{
    private static readonly AppDbContext _context =
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused.invalid").Options);

    private static IModel Model => _context.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Migrations_MatchTheModel()
    {
        Assert.False(_context.Database.HasPendingModelChanges(), "The model changed without a migration: run dotnet ef migrations add.");
    }

    [Fact]
    public void MigrationHistory_KeepsEfsOwnColumnNames()
    {
        // QA and prod created this table before snake_case names; renaming its columns breaks every later deploy.
        var script = _context.GetService<IHistoryRepository>().GetCreateScript();

        Assert.Contains("\"__EFMigrationsHistory\"", script, StringComparison.Ordinal);
        Assert.Contains("\"MigrationId\"", script, StringComparison.Ordinal);
        Assert.Contains("\"ProductVersion\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("migration_id", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Tables_AreTheDocumentedSet()
    {
        string[] expected =
        [
            "admin_fixes", "ai_usage", "collection_changes", "collection_imports", "collection_pokemon", "cp_multipliers",
            "egg_pool_entries", "event_bonuses", "event_moves", "event_pokemon", "events", "evolutions", "families",
            "form_moves", "forms", "job_runs", "keeper_filters", "max_bosses", "moves", "pvp_best_ivs", "pvp_keeper_boxes",
            "pvp_rankings", "raid_bosses", "raid_movesets", "raid_rankings", "refresh_tokens", "research_rewards", "research_tasks",
            "role_changes", "saved_filters", "species", "types", "unrecognised_rows", "users",
        ];

        Assert.Equal(expected, Model.GetEntityTypes().Select(entity => entity.GetTableName()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Names_AreSnakeCase()
    {
        foreach (var entity in Model.GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!);
            Assert.Matches(SnakeCase(), entity.GetTableName());
            foreach (var property in entity.GetProperties())
            {
                Assert.Matches(SnakeCase(), property.GetColumnName(table));
            }

            foreach (var name in entity.GetForeignKeys().Select(key => key.GetConstraintName())
                .Concat(entity.GetIndexes().Select(index => index.GetDatabaseName())))
            {
                Assert.Matches(SnakeCase(), name);
            }
        }
    }

    [Fact]
    public void Enums_AreStoredAsTheirNames()
    {
        var enums = Model.GetEntityTypes().SelectMany(entity => entity.GetProperties())
            .Where(property => Nullable.GetUnderlyingType(property.ClrType) is { IsEnum: true } || property.ClrType.IsEnum)
            .ToList();

        Assert.NotEmpty(enums);
        Assert.All(enums, property => Assert.Equal("text", property.GetColumnType()));
    }

    [Fact]
    public void ForeignKeys_AreIndexed()
    {
        // PostgreSQL doesn't index foreign keys by itself; deletes and joins need it.
        foreach (var key in Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()))
        {
            var columns = key.Properties.ToList();
            var covered = key.DeclaringEntityType.GetIndexes().Select(index => index.Properties)
                .Concat(key.DeclaringEntityType.GetKeys().Select(primary => primary.Properties))
                .Any(leading => leading.Take(columns.Count).SequenceEqual(columns));
            Assert.True(covered, $"{key.GetConstraintName()} has no index starting with its columns.");
        }
    }

    [Theory]
    [InlineData(typeof(SavedFilter))]
    [InlineData(typeof(CollectionImport))]
    [InlineData(typeof(CollectionPokemon))]
    [InlineData(typeof(RefreshToken))]
    public void UserData_IsDeletedWithTheUser(Type owned)
    {
        var key = Model.FindEntityType(owned)!.GetForeignKeys().Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User));

        Assert.Equal(DeleteBehavior.Cascade, key.DeleteBehavior);
    }

    [Fact]
    public void RoleHistory_GoesWithThePersonButKeepsWhoChangedIt()
    {
        var keys = Model.FindEntityType(typeof(RoleChange))!.GetForeignKeys().ToDictionary(key => key.Properties.Single().Name);

        Assert.Equal(DeleteBehavior.Cascade, keys[nameof(RoleChange.UserId)].DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, keys[nameof(RoleChange.ChangedByUserId)].DeleteBehavior);
    }

    [Fact]
    public void RecordsMentioningAUser_KeepTheRecordWhenTheUserIsDeleted()
    {
        var owned = new[] { typeof(SavedFilter), typeof(CollectionImport), typeof(CollectionPokemon), typeof(RefreshToken) };
        var others = Model.GetEntityTypes().Where(entity => !owned.Contains(entity.ClrType))
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(key => key.PrincipalEntityType.ClrType == typeof(User))
            .Where(key => !(key.DeclaringEntityType.ClrType == typeof(RoleChange) && key.Properties.Single().Name == nameof(RoleChange.UserId)))
            .ToList();

        Assert.NotEmpty(others);
        Assert.All(others, key => Assert.Equal(DeleteBehavior.SetNull, key.DeleteBehavior));
    }

    [Fact]
    public void LiveAndUserData_NeverLoseTheFormTheyPointAt()
    {
        var gameData = typeof(Form).Namespace;
        var references = Model.GetEntityTypes()
            .Where(entity => entity.ClrType.Namespace != gameData && !entity.ClrType.Namespace!.EndsWith(".Rankings", StringComparison.Ordinal))
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(key => key.PrincipalEntityType.ClrType.Namespace == gameData)
            .ToList();

        Assert.NotEmpty(references);
        Assert.All(references, key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
    }

    [Fact]
    public void FetchedEntries_HaveAUniqueKeyAndAJsonSnapshot()
    {
        var fetched = Model.GetEntityTypes().Where(entity => typeof(FetchedEntry).IsAssignableFrom(entity.ClrType)).ToList();

        Assert.Equal(5, fetched.Count);
        Assert.All(fetched, entity =>
        {
            Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(FetchedEntry.Key));
            Assert.Equal("jsonb", entity.FindProperty(nameof(FetchedEntry.SourceSnapshot))!.GetColumnType());
        });
    }

    [Fact]
    public void ScheduledTimes_AreWallClockTimes()
    {
        var scheduled = Model.GetEntityTypes().Where(entity => typeof(IScheduled).IsAssignableFrom(entity.ClrType)).ToList();

        Assert.Equal(3, scheduled.Count);
        Assert.All(scheduled.SelectMany(entity => new[] { entity.FindProperty(nameof(IScheduled.StartsAt))!, entity.FindProperty(nameof(IScheduled.EndsAt))! }),
            property => Assert.Equal("timestamp without time zone", property.GetColumnType()));
    }

    [Fact]
    public void Moments_AreStoredWithTheirTimeZone()
    {
        var moments = Model.GetEntityTypes().SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
            .ToList();

        Assert.NotEmpty(moments);
        Assert.All(moments, property => Assert.Equal("timestamp with time zone", property.GetColumnType()));
    }

    [Fact]
    public void Species_HaveOneDefaultForm()
    {
        var index = Model.FindEntityType(typeof(Form))!.GetIndexes().Single(index => index.GetDatabaseName() == "ix_forms_default_per_species");

        Assert.True(index.IsUnique);
        Assert.Equal("is_default", index.GetFilter());
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$")]
    private static partial Regex SnakeCase();
}
