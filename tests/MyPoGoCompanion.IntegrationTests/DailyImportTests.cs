using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyPoGoCompanion.Api.Freshness;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Data.Seed;
using MyPoGoCompanion.Jobs;
using MyPoGoCompanion.Jobs.DailyImport;
using Npgsql;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>
/// The daily import job on a real PostgreSQL 17 (E02-F01): the whole job as Azure runs it (command line, settings,
/// database), with the source files answered from the trimmed real fixtures instead of GitHub.
/// </summary>
public sealed class DailyImportTests(PostgresFixture postgres)
{
    private static readonly DailyImportOptions _defaults = new();
    private static readonly JsonSerializerOptions _json = new(JsonSerializerOptions.Web);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "ImportFixtures", name);

    private static Dictionary<string, string> Sources() => new()
    {
        [new Uri(_defaults.PvpokeBaseUrl, "data/gamemaster.json").AbsoluteUri] = "pvpoke-gamemaster.json",
        [new Uri(_defaults.PvpokeBaseUrl, "js/pokemon/Pokemon.js").AbsoluteUri] = "pvpoke-Pokemon.js",
        [new Uri(_defaults.PvpokeBaseUrl, "data/rankings/all/overall/rankings-1500.json").AbsoluteUri] = "pvpoke-rankings-1500.json",
        [new Uri(_defaults.PvpokeBaseUrl, "data/rankings/all/overall/rankings-2500.json").AbsoluteUri] = "pvpoke-rankings-2500.json",
        [new Uri(_defaults.PvpokeBaseUrl, "data/rankings/all/overall/rankings-10000.json").AbsoluteUri] = "pvpoke-rankings-10000.json",
        [_defaults.GameMasterUrl.AbsoluteUri] = "GAME_MASTER.json",
        [_defaults.GameTextUrl.AbsoluteUri] = "game-text-pt-br.json",
        [_defaults.GameTextRemoteUrl.AbsoluteUri] = "game-text-pt-br-remote.json",
    };

    /// <summary>Runs the job's command line against <paramref name="database"/>; returns its exit code.</summary>
    private static Task<int> RunJobAsync(string database, Dictionary<string, string>? sources = null, params string[] settings) =>
        JobHost.RunAsync(
            // The fixtures hold 28 species, not the whole Pokédex, and not 10 raid attackers of every type.
            ["daily-import", $"--ConnectionStrings:Database={database}", "--DailyImport:RetryDelay=00:00:00",
                "--DailyImport:FullPokedex=false", "--DailyImport:MinimumRaidRankingsPerType=0", .. settings],
            builder => builder.Services.AddHttpClient<SourceDownloader>(SourceDownloader.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new FixtureHttp(sources ?? Sources())),
            Ct);

    private static async Task<List<JobRun>> RunsAsync(string database)
    {
        await using var db = PostgresFixture.CreateContext(database);
        return await db.JobRuns.Where(run => run.Job == JobKind.DailyImport).OrderBy(run => run.StartedAt).ToListAsync(Ct);
    }

    [Fact]
    public async Task Import_FillsTheGameData_AndTheFreshnessSeesIt()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);

        Assert.Equal(0, await RunJobAsync(database, settings: "--DailyImport:Trigger=deploy"));

        await using (var db = PostgresFixture.CreateContext(database))
        {
            Assert.Equal(18, await db.Types.CountAsync(Ct));
            Assert.Equal(28, await db.Species.CountAsync(Ct));
            var charizard = await db.Forms.SingleAsync(form => form.Id == "charizard", Ct);
            Assert.Equal((3266, true, true), (charizard.MaxCp50, charizard.CanGigantamax, charizard.ShadowReleased));
            Assert.Equal("charizard", (await db.Forms.SingleAsync(form => form.Id == "charizard_mega_y", Ct)).BaseFormId);
            Assert.True(await db.Evolutions.AnyAsync(e => e.FromFormId == "eevee" && e.ToFormId == "espeon" && e.TimeOfDay == "day", Ct));
            Assert.True(await db.PvpRankings.AnyAsync(Ct));
            Assert.Equal(101, await db.CpMultipliers.CountAsync(Ct));

            // Portuguese names from the game text (E02-F02).
            Assert.Equal("Mega Charizard X", (await db.Forms.SingleAsync(form => form.Id == "charizard_mega_x", Ct)).NamePt);
            Assert.Equal("Vulpix de Alola", (await db.Forms.SingleAsync(form => form.Id == "vulpix_alolan", Ct)).NamePt);
            Assert.Equal("Chama Furacão", (await db.Moves.SingleAsync(move => move.Id == "FIRE_SPIN", Ct)).NamePt);
            Assert.Equal("Fada", (await db.Types.SingleAsync(type => type.Id == "fairy", Ct)).NamePt);
            Assert.False(await db.Species.AnyAsync(species => species.NamePt == null, Ct));
        }

        var run = Assert.Single(await RunsAsync(database));
        Assert.Equal(JobStatus.Succeeded, run.Status);
        var summary = ImportSummary.TryParse(run.Summary)!;
        Assert.Equal("deploy", summary.Trigger);
        Assert.Equal(43, summary.Changes["forms"].Added);

        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();
        var freshness = await client.GetFromJsonAsync<FreshnessResponse>(new Uri("/freshness", UriKind.Relative), _json, Ct);
        Assert.Equal(0, freshness!.GameData.DaysSinceUpdate);
        Assert.False(freshness.GameData.OutOfDate);
    }

    [Fact]
    public async Task Import_StoresTheEnginesResults()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        Assert.Equal(0, await RunJobAsync(database));

        await using var db = PostgresFixture.CreateContext(database);

        // The same numbers as the Python keeper build's golden files (tests/golden/keeper-filters) for Charizard.
        var great = await db.PvpBestIvs.SingleAsync(best => best.League == League.Great && best.FormId == "charizard", Ct);
        Assert.Equal((0, 15, 13, 19.5m, 1500), (great.AttackIv, great.DefenseIv, great.StaminaIv, great.Level, great.Cp));
        var master = await db.PvpBestIvs.SingleAsync(best => best.League == League.Master && best.FormId == "charizard", Ct);
        Assert.Equal((15, 15, 15, 50m, 3266), (master.AttackIv, master.DefenseIv, master.StaminaIv, master.Level, master.Cp));
        Assert.False(await db.PvpBestIvs.AnyAsync(best => best.FormId == "charizard_mega_y", Ct));

        var charmander = await db.PvpKeeperBoxes.SingleAsync(box => box.League == League.Ultra && box.Dex == 4, Ct);
        Assert.Equal((0, 1, 3, 4, 2, 4), (charmander.AttackBarMin, charmander.AttackBarMax, charmander.DefenseBarMin,
            charmander.DefenseBarMax, charmander.StaminaBarMin, charmander.StaminaBarMax));
        Assert.Equal(["charizard"], charmander.JudgedBy);
        Assert.Equal(28 * 2, await db.PvpKeeperBoxes.CountAsync(Ct));

        var fire = await db.RaidRankings.Where(ranking => ranking.AttackingTypeId == "fire").OrderBy(ranking => ranking.Rank).ToListAsync(Ct);
        Assert.Equal(Enumerable.Range(1, fire.Count), fire.Select(ranking => ranking.Rank));
        var shadow = Assert.Single(fire, ranking => ranking.FormId == "charizard" && ranking.IsShadow);
        Assert.Equal(("FIRE_SPIN", "BLAST_BURN"), (shadow.FastMoveId, shadow.ChargedMoveId));
        Assert.Equal(17.748826058703834, shadow.Dps);
        Assert.Equal(38.32352295847056, shadow.Score);

        var movesets = await db.RaidMovesets.Where(m => m.FormId == "charizard" && !m.IsShadow).OrderBy(m => m.Rank).ToListAsync(Ct);
        Assert.True(movesets.Count > 1);
        Assert.Equal(Enumerable.Range(1, movesets.Count), movesets.Select(m => m.Rank));
        Assert.True(movesets.Zip(movesets.Skip(1)).All(pair => pair.First.Score >= pair.Second.Score));

        var summary = ImportSummary.TryParse((await RunsAsync(database))[^1].Summary)!;
        Assert.Equal(await db.RaidMovesets.CountAsync(Ct), summary.Changes["raidMovesets"].Added);
    }

    [Fact]
    public async Task ImportAgain_SameSources_ChangesNothing()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await RunJobAsync(database);

        // Same sources and same version: nothing to do. A new version: everything compared again, nothing differs.
        Assert.Equal(0, await RunJobAsync(database));
        Assert.Equal(0, await RunJobAsync(database, settings: "--APP_VERSION=next"));

        var runs = await RunsAsync(database);
        Assert.Equal(3, runs.Count);
        Assert.True(ImportSummary.TryParse(runs[1].Summary)!.Unchanged);
        Assert.All(ImportSummary.TryParse(runs[2].Summary)!.Changes.Values, kind => Assert.Equal(0, kind.Total));
    }

    [Fact]
    public async Task WithoutTheGameText_TheImportSucceeds_AndKeepsThePortugueseNames()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await RunJobAsync(database);

        var noText = Sources();
        noText.Remove(_defaults.GameTextUrl.AbsoluteUri);
        Assert.Equal(0, await RunJobAsync(database, noText, "--APP_VERSION=next"));

        var run = (await RunsAsync(database))[^1];
        Assert.Equal(JobStatus.Succeeded, run.Status);
        var summary = ImportSummary.TryParse(run.Summary)!;
        Assert.Contains(summary.Warnings, warning => warning.StartsWith("Portuguese names not updated", StringComparison.Ordinal));
        Assert.All(summary.Changes.Values, kind => Assert.Equal(0, kind.Total));
        await using var db = PostgresFixture.CreateContext(database);
        Assert.Equal("Chama Furacão", (await db.Moves.SingleAsync(move => move.Id == "FIRE_SPIN", Ct)).NamePt);
        Assert.Equal("Mega Charizard X", (await db.Forms.SingleAsync(form => form.Id == "charizard_mega_x", Ct)).NamePt);
    }

    [Fact]
    public async Task AFailedDownload_LeavesYesterdaysData()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await RunJobAsync(database);
        int forms;
        await using (var db = PostgresFixture.CreateContext(database))
        {
            forms = await db.Forms.CountAsync(Ct);
        }

        var broken = Sources();
        broken.Remove(_defaults.GameMasterUrl.AbsoluteUri);
        Assert.Equal(1, await RunJobAsync(database, broken, "--APP_VERSION=next"));

        var failed = (await RunsAsync(database))[^1];
        Assert.Equal(JobStatus.Failed, failed.Status);
        Assert.StartsWith("Couldn't download the Game master", failed.Error, StringComparison.Ordinal);
        await using var check = PostgresFixture.CreateContext(database);
        Assert.Equal(forms, await check.Forms.CountAsync(Ct));
    }

    [Fact]
    public async Task AMainFormThatMoves_IsSaved_DespiteTheOneMainFormRule()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await RunJobAsync(database);
        await using (var db = PostgresFixture.CreateContext(database))
        {
            // As an older import would have had it: Zacian's Crowned Sword as the Pokédex line.
            await db.Forms.Where(f => f.Id == "zacian_hero").ExecuteUpdateAsync(set => set.SetProperty(f => f.IsDefault, false), Ct);
            await db.Forms.Where(f => f.Id == "zacian_crowned_sword").ExecuteUpdateAsync(set => set.SetProperty(f => f.IsDefault, true), Ct);
        }

        Assert.Equal(0, await RunJobAsync(database, settings: "--APP_VERSION=next"));

        await using var check = PostgresFixture.CreateContext(database);
        Assert.Equal("zacian_hero", await check.Forms.Where(f => f.Dex == 888 && f.IsDefault).Select(f => f.Id).SingleAsync(Ct));
    }

    [Fact]
    public async Task WhileAnotherRunHoldsTheLock_TheJobDoesNothing()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using var other = new NpgsqlConnection(database);
        await other.OpenAsync(Ct);
        await using (var take = new NpgsqlCommand($"select pg_advisory_lock({PostgresRunLock.DailyImportKey})", other))
        {
            await take.ExecuteNonQueryAsync(Ct);
        }

        Assert.Equal(0, await RunJobAsync(database));
        Assert.Empty(await RunsAsync(database));

        await using (var release = new NpgsqlCommand($"select pg_advisory_unlock({PostgresRunLock.DailyImportKey})", other))
        {
            await release.ExecuteNonQueryAsync(Ct);
        }

        Assert.Equal(0, await RunJobAsync(database));
        Assert.Single(await RunsAsync(database));
    }

    [Fact]
    public async Task OnTheDevSeed_ImportsAlongsideLiveDataAndCollections()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using (var db = PostgresFixture.CreateContext(database))
        {
            await DevSeed.SeedAsync(db, DateTimeOffset.UtcNow, Ct);
        }

        // The seed holds more species than the fixtures; the first import into a database publishes their removal.
        Assert.Equal(0, await RunJobAsync(database));

        await using var check = PostgresFixture.CreateContext(database);
        var kyogre = await check.Forms.SingleAsync(f => f.Id == "kyogre", Ct);
        Assert.False(kyogre.Released); // not in the fixtures: kept (raids and collections point at it), not released
        Assert.True(await check.RaidBosses.AnyAsync(boss => boss.FormId == "kyogre", Ct));
        Assert.True((await check.Forms.SingleAsync(f => f.Id == "charizard", Ct)).Released);
        var run = (await RunsAsync(database))[^1];
        Assert.Equal(JobStatus.Succeeded, run.Status);
        var summary = JsonSerializer.Deserialize<ImportSummary>(run.Summary!, _json)!;
        Assert.True(summary.Removals["forms"].Count > 0); // the seed's forms the fixtures lack, Kyogre among them
        Assert.False(summary.PublishedMissing);
    }

    [Fact]
    public async Task MissingRows_HoldTheRun_UntilTheSuperAdminPublishesThem()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await RunJobAsync(database);
        var admin = Guid.CreateVersion7();
        int released;
        await using (var db = PostgresFixture.CreateContext(database))
        {
            db.Users.Add(new Data.Users.User { Id = admin, GoogleSubject = "google-admin", Email = "admin@example.test", DisplayName = "Admin" });
            await db.SaveChangesAsync(Ct);
            released = await db.Forms.CountAsync(form => form.Released, Ct);
        }

        // PvPoke no longer lists Alolan Vulpix.
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(await File.ReadAllBytesAsync(Fixture("pvpoke-gamemaster.json"), Ct), _json)!;
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId == "vulpix_alolan");
        var folder = Directory.CreateTempSubdirectory("import-").FullName;
        var changed = Path.Combine(folder, "pvpoke-gamemaster.json");
        await File.WriteAllBytesAsync(changed, JsonSerializer.SerializeToUtf8Bytes(pvpoke, _json), Ct);
        var sources = Sources();
        sources[new Uri(_defaults.PvpokeBaseUrl, "data/gamemaster.json").AbsoluteUri] = changed;

        Assert.Equal(0, await RunJobAsync(database, sources, "--APP_VERSION=next"));
        var held = (await RunsAsync(database))[^1];
        Assert.Equal((JobStatus.Held, null), (held.Status, held.Error));
        Assert.NotNull(held.FinishedAt);
        await using (var db = PostgresFixture.CreateContext(database))
        {
            Assert.Equal(released, await db.Forms.CountAsync(form => form.Released, Ct)); // nothing published
            // A held run doesn't count as an update (the out-of-date warning reads the last successful run).
            var lastSuccess = await db.JobRuns.Where(r => r.Job == JobKind.DailyImport && r.Status == JobStatus.Succeeded).MaxAsync(r => r.FinishedAt, Ct);
            Assert.True(lastSuccess < held.StartedAt);
        }

        Assert.Equal(0, await RunJobAsync(database, sources, "--APP_VERSION=next", "--DailyImport:PublishMissing=true", $"--DailyImport:RequestedBy={admin}", "--DailyImport:Trigger=admin"));
        var published = (await RunsAsync(database))[^1];
        Assert.Equal((JobStatus.Succeeded, admin), (published.Status, published.RequestedByUserId));
        var summary = JsonSerializer.Deserialize<ImportSummary>(published.Summary!, _json)!;
        Assert.Equal(("admin", true), (summary.Trigger, summary.PublishedMissing));
        await using var check = PostgresFixture.CreateContext(database);
        Assert.False((await check.Forms.SingleAsync(f => f.Id == "vulpix_alolan", Ct)).Released);
    }

    /// <summary>Answers the source addresses with the fixture files; anything else is 404.</summary>
    private sealed class FixtureHttp(Dictionary<string, string> files) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            files.TryGetValue(request.RequestUri!.AbsoluteUri, out var name)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(await File.ReadAllBytesAsync(Fixture(name), cancellationToken)) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
    }
}
