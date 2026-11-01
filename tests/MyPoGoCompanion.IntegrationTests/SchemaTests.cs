using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Seed;
using MyPoGoCompanion.Data.Users;
using Npgsql;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>
/// The schema's rules as PostgreSQL enforces them: what deleting a user or a form does, and the checks that keep
/// bad values out. Each test seeds its own database with the dev sample data.
/// </summary>
public sealed class SchemaTests(PostgresFixture postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<AppDbContext> SeededAsync()
    {
        var context = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync(Cancel));
        await DevSeed.SeedAsync(context, new DateTimeOffset(DevSeed.SampleNow, TimeSpan.Zero), Cancel);
        context.ChangeTracker.Clear();
        return context;
    }

    [Fact]
    public async Task DeletingAUser_DeletesWhatTheyOwn_AndKeepsTheRecordsThatMentionThem()
    {
        await using var db = await SeededAsync();
        var ash = await db.Users.SingleAsync(Cancel);
        var raid = await db.RaidBosses.FirstAsync(Cancel);
        raid.FixedAt = DateTimeOffset.UtcNow;
        raid.FixedByUserId = ash.Id;
        db.AdminFixes.Add(new AdminFix { At = DateTimeOffset.UtcNow, UserId = ash.Id, EntryTable = "raid_bosses", EntryId = raid.Id });
        await db.SaveChangesAsync(Cancel);

        await db.Users.Where(user => user.Id == ash.Id).ExecuteDeleteAsync(Cancel);

        Assert.Equal(0, await db.CollectionPokemon.CountAsync(Cancel));
        Assert.Equal(0, await db.CollectionImports.CountAsync(Cancel));
        Assert.Equal(0, await db.CollectionChanges.CountAsync(Cancel));
        Assert.Equal(0, await db.UnrecognisedRows.CountAsync(Cancel));
        Assert.Equal(0, await db.SavedFilters.CountAsync(Cancel));
        Assert.Equal(2, await db.AiUsage.CountAsync(Cancel));
        Assert.True(await db.AiUsage.AllAsync(usage => usage.UserId == null, Cancel));
        var fixedRaid = await db.RaidBosses.AsNoTracking().SingleAsync(boss => boss.Id == raid.Id, Cancel);
        Assert.Null(fixedRaid.FixedByUserId);
        Assert.NotNull(fixedRaid.FixedAt);
        Assert.Null((await db.AdminFixes.AsNoTracking().SingleAsync(Cancel)).UserId);
    }

    [Fact]
    public async Task DeletingAnUpload_KeepsTheCollection()
    {
        await using var db = await SeededAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.CollectionImports.ExecuteDeleteAsync(Cancel));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Theory]
    [InlineData("garchomp")] // in Ash's collection
    [InlineData("kyogre")] // a raid boss
    public async Task DeletingAFormThatSomeoneUses_IsRefused(string formId)
    {
        await using var db = await SeededAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Forms.Where(form => form.Id == formId).ExecuteDeleteAsync(Cancel));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
    }

    [Fact]
    public async Task DeletingAFormNobodyUses_DeletesItsMovesAndRankings()
    {
        await using var db = await SeededAsync();
        Assert.True(await db.RaidRankings.AnyAsync(ranking => ranking.FormId == "garchomp_mega", Cancel));

        await db.Forms.Where(form => form.Id == "garchomp_mega").ExecuteDeleteAsync(Cancel);

        Assert.False(await db.FormMoves.AnyAsync(learn => learn.FormId == "garchomp_mega", Cancel));
        Assert.False(await db.RaidRankings.AnyAsync(ranking => ranking.FormId == "garchomp_mega", Cancel));
        Assert.True(await db.Forms.AnyAsync(form => form.Id == "garchomp", Cancel));
    }

    [Fact]
    public async Task DeletingAnEvent_DeletesItsDetails_AndUnlinksItsRaids()
    {
        await using var db = await SeededAsync();
        var halloween = await db.Events.SingleAsync(gameEvent => gameEvent.Key == "event/halloween-2026", Cancel);

        await db.Events.Where(gameEvent => gameEvent.Id == halloween.Id).ExecuteDeleteAsync(Cancel);

        Assert.False(await db.EventBonuses.AnyAsync(bonus => bonus.EventId == halloween.Id, Cancel));
        Assert.False(await db.EventPokemon.AnyAsync(featured => featured.EventId == halloween.Id, Cancel));
        Assert.True(await db.RaidBosses.AnyAsync(boss => boss.FormId == "gengar_mega" && boss.EventId == null, Cancel));
        Assert.True(await db.ResearchTasks.AnyAsync(task => task.Key == "research/catch-10-ghost" && task.EventId == null, Cancel));
    }

    public static TheoryData<string, string> RejectedUpdates() => new()
    {
        { "update collection_pokemon set attack_iv = 16", "ck_collection_pokemon_ivs" },
        { "update collection_pokemon set level = 20.25", "ck_collection_pokemon_level" },
        { "update collection_pokemon set level = 51.5", "ck_collection_pokemon_level" },
        { "update collection_pokemon set cp = 9", "ck_collection_pokemon_cp" },
        { "update pvp_best_ivs set stamina_iv = -1", "ck_pvp_best_ivs_ivs" },
        { "update forms set attack = 0", "ck_forms_stats" },
        { "update forms set type2_id = type1_id", "ck_forms_two_different_types" },
        { "update max_bosses set stars = 7", "ck_max_bosses_stars" },
        { "update raid_bosses set ends_at = starts_at", "ck_raid_bosses_ends_after_start" },
        { "update events set ends_at = starts_at - interval '1 hour'", "ck_events_ends_after_start" },
        { "update users set language = 'pt-BR'", "ck_users_language" },
        { "update saved_filters set name = ''", "ck_saved_filters_name" },
        { "update pvp_keeper_boxes set attack_bar_min = 3, attack_bar_max = 2", "ck_pvp_keeper_boxes_bars" },
        { "update job_runs set finished_at = null", "ck_job_runs_finished" },
        { "update ai_usage set cost_usd = -1", "ck_ai_usage_amounts" },
        { "update collection_changes set before = null where kind = 'Gone'", "ck_collection_changes_before_after" },
        { "update evolutions set time_of_day = 'dusk'", "ck_evolutions_time_of_day" },
        { "update moves set pvp_buff_chance = 1.5 where pvp_buff_chance is not null", "ck_moves_buff_chance" },
    };

    [Theory]
    [MemberData(nameof(RejectedUpdates))]
    public async Task CheckConstraints_RejectBadValues(string sql, string constraint)
    {
        await using var db = await SeededAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Cancel));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    [Fact]
    public async Task Species_CannotHaveTwoDefaultForms()
    {
        await using var db = await SeededAsync();

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("update forms set is_default = true where id = 'garchomp_mega'", Cancel));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        Assert.Equal("ix_forms_default_per_species", error.ConstraintName);
    }

    [Fact]
    public async Task FetchedEntries_CannotShareAKey()
    {
        await using var db = await SeededAsync();
        db.RaidBosses.Add(new RaidBoss
        {
            Key = (await db.RaidBosses.FirstAsync(Cancel)).Key,
            FormId = "lapras",
            Tier = RaidTier.ThreeStar,
            StartsAt = DevSeed.SampleNow,
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Cancel));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Users_AreUniquePerGoogleAccount()
    {
        await using var db = await SeededAsync();
        db.Users.Add(new User { GoogleSubject = DevSeed.SampleGoogleSubject, Email = "other@example.com", DisplayName = "Other" });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Cancel));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Values_AreStoredReadably()
    {
        await using var db = await SeededAsync();

        // Enums as names, lists as arrays, JSON as jsonb, wall-clock times without a zone.
        Assert.Equal("FiveStar", await ScalarAsync<string>(db, "select tier from raid_bosses where form_id = 'kyogre'"));
        Assert.Equal(["chomp"], await ScalarAsync<string[]>(db, "select nicknames from species where dex = 445"));
        Assert.Equal("powered-up", await ScalarAsync<string>(db, "select what[1] from collection_changes where kind = 'Changed'"));
        Assert.Equal(23, await ScalarAsync<int>(db, "select (after ->> 'level')::int from collection_changes where kind = 'Changed'"));
        Assert.Equal(new DateTime(2026, 11, 7, 14, 0, 0), await ScalarAsync<DateTime>(db, "select starts_at from events where kind = 'CommunityDay'"));
        Assert.Equal(20.5m, await ScalarAsync<decimal>(db, "select level from cp_multipliers where level = 20.5"));
    }

    [Fact]
    public async Task SeededData_ReadsBackThroughTheModel()
    {
        await using var db = await SeededAsync();

        var communityDay = await db.Events.Include(gameEvent => gameEvent.Moves).ThenInclude(eventMove => eventMove.Move)
            .Include(gameEvent => gameEvent.Bonuses)
            .SingleAsync(gameEvent => gameEvent.Kind == EventKind.CommunityDay, Cancel);
        var garchomp = await db.Forms.Include(form => form.Species).ThenInclude(species => species.Family)
            .Include(form => form.Moves).SingleAsync(form => form.Id == "garchomp", Cancel);
        var collection = await db.CollectionPokemon.Include(pokemon => pokemon.Form).Where(pokemon => pokemon.Form.Dex == 443)
            .ToListAsync(Cancel);

        Assert.Equal("Earth Power", communityDay.Moves.Single().Move.NameEn);
        Assert.Equal(3, communityDay.Bonuses.Count);
        Assert.Equal("FAMILY_GIBLE", garchomp.Species.Family.Id);
        Assert.Contains(garchomp.Moves, learn => learn.MoveId == "EARTH_POWER" && learn.Availability == MoveAvailability.EliteTm);
        Assert.Equal(4, collection.Count);
    }

    private static async Task<T> ScalarAsync<T>(AppDbContext db, string sql)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(Cancel);
        }

        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(Cancel))!;
    }
}
