using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>
/// One run of the import on an in-memory database: the run log, the "nothing changed" shortcut, failures that leave
/// the data as it was, the guard against cut-off files and the lock. The real PostgreSQL runs are in the integration
/// tests (DailyImportTests).
/// </summary>
public sealed class DailyImportJobTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 6, 0, 0, TimeSpan.Zero);
    // The fixtures hold 28 species, not the whole Pokédex, and not 10 raid attackers of every type.
    private static readonly DailyImportOptions _options = Options();

    private static DailyImportOptions Options(bool publishMissing = false, Guid? requestedBy = null) => new()
    {
        RetryDelay = TimeSpan.Zero,
        Trigger = "schedule",
        FullPokedex = false,
        MinimumRaidRankingsPerType = 0,
        PublishMissing = publishMissing,
        RequestedBy = requestedBy,
    };

    private readonly string _database = Guid.NewGuid().ToString();
    private readonly FakeHttp _http = new(Fixtures.Responses(_options.PvpokeBaseUrl, _options.GameMasterUrl));
    private readonly FakeTime _time = new(_now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(_database)
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private async Task<DailyImportOutcome> RunAsync(string version = "abc1234", IRunLock? runLock = null, DailyImportOptions? options = null, CancellationToken? cancellationToken = null)
    {
        await using var db = Db();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["APP_VERSION"] = version }).Build();
        var downloader = new SourceDownloader(new HttpClient(_http), Microsoft.Extensions.Options.Options.Create(options ?? _options), NullLogger<SourceDownloader>.Instance);
        var job = new DailyImportJob(db, runLock ?? new FreeLock(), downloader, Microsoft.Extensions.Options.Options.Create(options ?? _options), configuration, _time, NullLogger<DailyImportJob>.Instance);
        return await job.RunAsync(cancellationToken ?? Ct);
    }

    private async Task<List<JobRun>> RunsAsync()
    {
        await using var db = Db();
        return await db.JobRuns.OrderBy(run => run.StartedAt).ThenBy(run => run.Id).ToListAsync(Ct);
    }

    private static ImportSummary Summary(JobRun run) => ImportSummary.TryParse(run.Summary)!;

    [Fact]
    public async Task FirstRun_ImportsEverything_AndLogsTheRun()
    {
        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync());

        var run = Assert.Single(await RunsAsync());
        Assert.Equal((JobKind.DailyImport, JobStatus.Succeeded, _now, _now, null), (run.Job, run.Status, run.StartedAt, run.FinishedAt, run.Error));
        var summary = Summary(run);
        Assert.Equal(("schedule", "abc1234", false), (summary.Trigger, summary.Version, summary.Unchanged));
        Assert.Equal(8, summary.Sources.Count);
        Assert.Equal(Fixtures.Bytes("GAME_MASTER.json").Length, summary.Sources["gameMaster"].Bytes);
        Assert.Equal(Fixtures.Built.Forms.Count, summary.Changes["forms"].Added);
        Assert.Equal(18, summary.Changes["types"].Added);
        Assert.Contains(summary.Warnings, warning => warning.Contains("basculegion", StringComparison.Ordinal));

        await using var db = Db();
        Assert.Equal(Fixtures.Built.Forms.Count, await db.Forms.CountAsync(Ct));
        Assert.Equal(Fixtures.Built.Evolutions.Count, await db.Evolutions.CountAsync(Ct));
        Assert.Equal(Fixtures.Built.PvpRankings.Count, await db.PvpRankings.CountAsync(Ct));
    }

    private static string Sprite(string name) => _options.SpriteBaseUrl.AbsoluteUri + name + ".icon.png";

    [Fact]
    public async Task TheImport_StoresEachFormsPictures_AndTrustsThemNextTime()
    {
        foreach (var name in new[] { "pm6", "pm6.s", "pm6.fGIGANTAMAX", "pm6.fMEGA_Y" })
        {
            _http.Responses[Sprite(name)] = () => new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }

        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync());

        await using (var db = Db())
        {
            var charizard = await db.Forms.SingleAsync(form => form.Id == "charizard", Ct);
            Assert.Equal(("pm6", "pm6.s", "pm6.fGIGANTAMAX"), (charizard.Sprite, charizard.SpriteShiny, charizard.SpriteGigantamax));
            var megaY = await db.Forms.SingleAsync(form => form.Id == "charizard_mega_y", Ct);
            Assert.Equal(("pm6.fMEGA_Y", null), (megaY.Sprite, megaY.SpriteShiny));
            // PokeMiners has no Mega X picture in this test: it shows Charizard's.
            var megaX = await db.Forms.SingleAsync(form => form.Id == "charizard_mega_x", Ct);
            Assert.Equal(("pm6", "pm6.s"), (megaX.Sprite, megaX.SpriteShiny));
        }

        var warnings = Summary((await RunsAsync())[0]).Warnings;
        Assert.Contains(warnings, warning => warning.Contains("show their main form's picture", StringComparison.Ordinal) && warning.Contains("charizard_mega_x", StringComparison.Ordinal));

        _http.Requests.Clear();
        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync("next"));
        Assert.DoesNotContain(Sprite("pm6"), _http.Requests);
        Assert.Contains(Sprite("pm6.fMEGA_X"), _http.Requests);
    }

    [Fact]
    public async Task SameSourcesAndVersion_NothingToDo_StillASuccess()
    {
        await RunAsync();
        _time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(DailyImportOutcome.Unchanged, await RunAsync());

        var second = (await RunsAsync())[1];
        Assert.Equal(JobStatus.Succeeded, second.Status);
        Assert.True(Summary(second).Unchanged);
        Assert.Empty(Summary(second).Changes);
    }

    [Fact]
    public async Task ANewVersion_ImportsAgain_WithNoChanges()
    {
        await RunAsync();
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync(version: "def5678"));

        var summary = Summary((await RunsAsync())[1]);
        Assert.False(summary.Unchanged);
        Assert.All(summary.Changes.Values, kind => Assert.Equal(0, kind.Total));
    }

    [Fact]
    public async Task ADownloadFailure_FailsTheRun_AndChangesNothing()
    {
        await RunAsync();
        _http.Responses[_options.GameMasterUrl.AbsoluteUri] = () => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        _time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(DailyImportOutcome.Failed, await RunAsync(version: "def5678"));

        var failed = (await RunsAsync())[1];
        Assert.Equal((JobStatus.Failed, _now.AddDays(1)), (failed.Status, failed.FinishedAt));
        Assert.StartsWith("Couldn't download the Game master", failed.Error, StringComparison.Ordinal);
        Assert.Empty(Summary(failed).Changes);
    }

    [Fact]
    public async Task MissingRows_HoldTheRun_AndChangeNothing()
    {
        await RunAsync();
        // A broken PvPoke game master that still reads: every Shadow and Mega is gone.
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(Fixtures.Bytes("pvpoke-gamemaster.json"), JsonSerializerOptions.Web)!;
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId.EndsWith("_shadow", StringComparison.Ordinal) || p.SpeciesId.Contains("_mega", StringComparison.Ordinal));
        ServePvpoke(pvpoke);
        _time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(DailyImportOutcome.Held, await RunAsync(version: "def5678"));

        var held = (await RunsAsync())[1];
        Assert.Equal((JobStatus.Held, _now.AddDays(1), null), (held.Status, held.FinishedAt, held.Error));
        var summary = Summary(held);
        Assert.True(summary.Removals["forms"].Count >= 3); // the Megas, kept but no longer released
        Assert.True(summary.Removals["raidMovesets"].Count > RemovalList.Shown);
        Assert.Equal(RemovalList.Shown, summary.Removals["raidMovesets"].Items.Count);
        Assert.NotNull(summary.RemovalsFingerprint);
        Assert.False(summary.PublishedMissing);
        Assert.All(summary.Checks, check => Assert.True(check.Passed));
        await using var db = Db();
        Assert.Equal(Fixtures.Built.Forms.Count(form => form.Released), await db.Forms.CountAsync(form => form.Released, Ct));
        Assert.Equal(Fixtures.Built.PvpRankings.Count, await db.PvpRankings.CountAsync(Ct));
    }

    [Fact]
    public async Task ACutOffSource_FailsThePokedexCheck()
    {
        await RunAsync();
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(Fixtures.Bytes("pvpoke-gamemaster.json"), JsonSerializerOptions.Web)!;
        pvpoke.Pokemon.RemoveRange(pvpoke.Pokemon.Count / 2, pvpoke.Pokemon.Count - (pvpoke.Pokemon.Count / 2));
        ServePvpoke(pvpoke);

        Assert.Equal(DailyImportOutcome.Failed, await RunAsync(version: "def5678"));

        var failed = (await RunsAsync())[1];
        Assert.Contains("Pokédex: The highest Pokédex number is #", failed.Error, StringComparison.Ordinal);
        Assert.Contains("lower than the #1000 already stored.", failed.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrokenData_FailsTheChecks_AndChangesNothing()
    {
        await RunAsync();
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(Fixtures.Bytes("pvpoke-gamemaster.json"), JsonSerializerOptions.Web)!;
        pvpoke.Pokemon.Single(p => p.SpeciesId == "charizard").BaseStats.Atk = 0;
        ServePvpoke(pvpoke);
        _time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(DailyImportOutcome.Failed, await RunAsync(version: "def5678"));

        var failed = (await RunsAsync())[1];
        Assert.StartsWith("The new data failed 1 sanity check, so nothing was published (yesterday's data stays live). Numbers: Charizard (charizard) has base stats 0/", failed.Error, StringComparison.Ordinal);
        var summary = Summary(failed);
        Assert.False(summary.Checks.Single(check => check.Name == "Numbers").Passed);
        Assert.Empty(summary.Changes);
        await using var db = Db();
        Assert.Equal(223, (await db.Forms.SingleAsync(f => f.Id == "charizard", Ct)).Attack);
    }

    [Fact]
    public async Task PublishWhatsMissing_PublishesTheRemovalsTheHeldRunShowed()
    {
        var admin = Guid.CreateVersion7();
        await using (var db = Db())
        {
            db.Users.Add(new Data.Users.User { Id = admin, GoogleSubject = "google-admin", Email = "admin@example.test", DisplayName = "Admin" });
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync();
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(Fixtures.Bytes("pvpoke-gamemaster.json"), JsonSerializerOptions.Web)!;
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId == "vulpix_alolan");
        ServePvpoke(pvpoke);
        _time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(DailyImportOutcome.Held, await RunAsync(version: "def5678"));
        var held = Summary((await RunsAsync())[1]);
        Assert.Equal(["Vulpix (Alolan)"], held.Removals["forms"].Items);

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync(version: "def5678", options: Options(publishMissing: true, requestedBy: admin)));

        var published = (await RunsAsync())[2];
        Assert.Equal((JobStatus.Succeeded, admin), (published.Status, published.RequestedByUserId));
        var summary = Summary(published);
        Assert.True(summary.PublishedMissing);
        Assert.Equal(held.RemovalsFingerprint, summary.RemovalsFingerprint);
        Assert.Equal(1, summary.Changes["forms"].Removed);
        await using var check = Db();
        Assert.False((await check.Forms.SingleAsync(f => f.Id == "vulpix_alolan", Ct)).Released);
    }

    [Fact]
    public async Task PublishWhatsMissing_HoldsAgain_WhenTheRemovalsChanged()
    {
        await RunAsync();
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(Fixtures.Bytes("pvpoke-gamemaster.json"), JsonSerializerOptions.Web)!;
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId == "vulpix_alolan");
        ServePvpoke(pvpoke);
        _time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(DailyImportOutcome.Held, await RunAsync(version: "def5678"));

        // Overnight something else went missing too: what was accepted isn't what would go now.
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId == "bulbasaur");
        ServePvpoke(pvpoke);
        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(DailyImportOutcome.Held, await RunAsync(version: "def5678", options: Options(publishMissing: true, requestedBy: Guid.CreateVersion7())));

        var again = (await RunsAsync())[2];
        Assert.Equal((JobStatus.Held, null), (again.Status, again.RequestedByUserId)); // an unknown user isn't recorded
        Assert.Contains(Summary(again).Warnings, w => w.Contains("the rows missing changed since the held run you saw", StringComparison.Ordinal));
        await using var db = Db();
        Assert.True((await db.Forms.SingleAsync(f => f.Id == "vulpix_alolan", Ct)).Released);
    }

    [Fact]
    public async Task PublishWhatsMissing_WithNothingWaiting_Holds()
    {
        await RunAsync();
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(Fixtures.Bytes("pvpoke-gamemaster.json"), JsonSerializerOptions.Web)!;
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId == "vulpix_alolan");
        ServePvpoke(pvpoke);

        Assert.Equal(DailyImportOutcome.Held, await RunAsync(version: "def5678", options: Options(publishMissing: true)));

        Assert.Contains(Summary((await RunsAsync())[1]).Warnings, w => w.Contains("no run was waiting for a decision", StringComparison.Ordinal));
    }

    private void ServePvpoke(PvpokeGameMaster pvpoke) =>
        _http.Responses[new Uri(_options.PvpokeBaseUrl, "data/gamemaster.json").AbsoluteUri] = () =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(pvpoke, JsonSerializerOptions.Web)) };

    [Fact]
    public async Task ARunThatNeverFinished_IsMarkedFailed()
    {
        await using (var db = Db())
        {
            db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Running, StartedAt = _now.AddDays(-1) });
            db.JobRuns.Add(new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Running, StartedAt = _now.AddDays(-1) });
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync();

        var runs = await RunsAsync();
        var stopped = runs.Single(run => run.Job == JobKind.DailyImport && run.StartedAt < _now);
        Assert.Equal((JobStatus.Failed, _now, DailyImportJob.StoppedMessage), (stopped.Status, stopped.FinishedAt, stopped.Error));
        Assert.Equal(JobStatus.Running, runs.Single(run => run.Job == JobKind.AiResearcher).Status);
    }

    [Fact]
    public async Task AnotherRunHoldingTheLock_NothingHappens()
    {
        Assert.Equal(DailyImportOutcome.AlreadyRunning, await RunAsync(runLock: new TakenLock()));

        Assert.Empty(await RunsAsync());
        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async Task TheLock_IsReleasedAfterTheRun()
    {
        var runLock = new FreeLock();

        await RunAsync(runLock: runLock);

        Assert.True(runLock.Released);
    }

    [Fact]
    public async Task Stopping_MarksTheRunFailed()
    {
        using var cancel = new CancellationTokenSource();
        _http.Responses[_options.GameMasterUrl.AbsoluteUri] = () =>
        {
            cancel.Cancel();
            throw new OperationCanceledException(cancel.Token);
        };

        Assert.Equal(DailyImportOutcome.Failed, await RunAsync(cancellationToken: cancel.Token));

        var run = Assert.Single(await RunsAsync());
        Assert.Equal((JobStatus.Failed, DailyImportJob.StoppedMessage), (run.Status, run.Error));
    }

    [Fact]
    public async Task WhatOtherJobsFillIn_IsKept()
    {
        await RunAsync();
        await using (var db = Db())
        {
            var charizard = await db.Forms.SingleAsync(form => form.Id == "charizard", Ct);
            charizard.ShinyReleased = true;
            (await db.Evolutions.FirstAsync(Ct)).TaskPt = "Tarefa";
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync(version: "def5678");

        await using var check = Db();
        Assert.True((await check.Forms.SingleAsync(f => f.Id == "charizard", Ct)).ShinyReleased);
        Assert.Contains(await check.Evolutions.ToListAsync(Ct), e => e.TaskPt == "Tarefa");
        Assert.All(Summary((await RunsAsync())[1]).Changes.Values, kind => Assert.Equal(0, kind.Total));
    }

    [Fact]
    public async Task PortugueseNames_ComeFromTheGameText()
    {
        await RunAsync();

        await using var db = Db();
        Assert.Equal("Vulpix de Alola", (await db.Forms.SingleAsync(f => f.Id == "vulpix_alolan", Ct)).NamePt);
        Assert.Equal("Chama Furacão", (await db.Moves.SingleAsync(m => m.Id == "FIRE_SPIN", Ct)).NamePt);
        Assert.Equal("Planta", (await db.Types.SingleAsync(t => t.Id == "grass", Ct)).NamePt);
    }

    [Fact]
    public async Task WithoutTheGameText_TheImportGoesOn_AndKeepsThePortugueseNames()
    {
        await RunAsync();
        await using (var db = Db())
        {
            // A name the game text had yesterday (made up here, to see it is kept rather than looked up again).
            (await db.Moves.SingleAsync(m => m.Id == "FIRE_SPIN", Ct)).NamePt = "Nome de ontem";
            await db.SaveChangesAsync(Ct);
        }

        _http.Responses.Remove(_options.GameTextUrl.AbsoluteUri);
        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync(version: "def5678"));

        var run = (await RunsAsync())[1];
        Assert.Equal(JobStatus.Succeeded, run.Status);
        var summary = Summary(run);
        Assert.Equal(6, summary.Sources.Count);
        Assert.Contains(summary.Warnings, w => w.StartsWith("Portuguese names not updated, the last ones are kept: Couldn't download the Portuguese game text", StringComparison.Ordinal));
        Assert.All(summary.Changes.Values, kind => Assert.Equal(0, kind.Total));
        await using var check = Db();
        Assert.Equal("Nome de ontem", (await check.Moves.SingleAsync(m => m.Id == "FIRE_SPIN", Ct)).NamePt);
        Assert.Equal("Vulpix de Alola", (await check.Forms.SingleAsync(f => f.Id == "vulpix_alolan", Ct)).NamePt);
        Assert.Equal("Fada", (await check.Types.SingleAsync(t => t.Id == "fairy", Ct)).NamePt);
    }

    [Fact]
    public async Task TheGameTextComingBack_IsNotTakenAsUnchanged()
    {
        _http.Responses.Remove(_options.GameTextUrl.AbsoluteUri);
        await RunAsync();
        _http.Responses[_options.GameTextUrl.AbsoluteUri] = () => Fixtures.Ok("game-text-pt-br.json");

        await RunAsync();

        var runs = await RunsAsync();
        Assert.False(Summary(runs[1]).Unchanged);
        await using var db = Db();
        Assert.Equal("Chama Furacão", (await db.Moves.SingleAsync(m => m.Id == "FIRE_SPIN", Ct)).NamePt);
    }

    [Fact]
    public async Task ChangesInTheSources_AreCountedPerKind()
    {
        await RunAsync();
        var pvpoke = JsonSerializer.Deserialize<PvpokeGameMaster>(Fixtures.Bytes("pvpoke-gamemaster.json"), JsonSerializerOptions.Web)!;
        var charizard = pvpoke.Pokemon.Single(p => p.SpeciesId == "charizard");
        charizard.BaseStats.Atk += 10; // a rebalance
        charizard.FastMoves.Remove("WING_ATTACK"); // a move removed from its pool
        charizard.EliteMoves.Remove("WING_ATTACK");
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId == "vulpix_alolan"); // gone from PvPoke
        pvpoke.Pokemon.RemoveAll(p => p.SpeciesId == "bulbasaur");
        ServePvpoke(pvpoke);

        // Rows go missing, so the run is held, then published with the super admin's OK.
        Assert.Equal(DailyImportOutcome.Held, await RunAsync(version: "def5678"));
        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync(version: "def5678", options: Options(publishMissing: true)));

        var changes = Summary((await RunsAsync())[2]).Changes;
        Assert.Equal(2, changes["forms"].Removed); // Alolan Vulpix and Bulbasaur: kept, marked not released
        Assert.True(changes["forms"].Changed >= 1);
        Assert.True(changes["movepools"].Removed >= 1);
        await using var db = Db();
        Assert.False((await db.Forms.SingleAsync(f => f.Id == "vulpix_alolan", Ct)).Released);
        Assert.Equal(233, (await db.Forms.SingleAsync(f => f.Id == "charizard", Ct)).Attack);
        Assert.DoesNotContain(await db.FormMoves.Where(m => m.FormId == "charizard").Select(m => m.MoveId).ToListAsync(Ct), id => id == "WING_ATTACK");
        // The forms PvPoke no longer lists keep their movepool.
        Assert.NotEmpty(await db.FormMoves.Where(m => m.FormId == "vulpix_alolan").ToListAsync(Ct));
    }

    [Fact]
    public async Task AMainFormThatMoves_IsSwitchedAndCounted()
    {
        await RunAsync();
        await using (var db = Db())
        {
            // As if an earlier import had picked Zacian's Crowned Sword as the Pokédex line.
            (await db.Forms.SingleAsync(f => f.Id == "zacian_hero", Ct)).IsDefault = false;
            (await db.Forms.SingleAsync(f => f.Id == "zacian_crowned_sword", Ct)).IsDefault = true;
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync(version: "def5678");

        await using var check = Db();
        Assert.True((await check.Forms.SingleAsync(f => f.Id == "zacian_hero", Ct)).IsDefault);
        Assert.False((await check.Forms.SingleAsync(f => f.Id == "zacian_crowned_sword", Ct)).IsDefault);
        Assert.Equal(2, Summary((await RunsAsync())[1]).Changes["forms"].Changed);
    }

    [Theory]
    [InlineData("plain words", "plain words")]
    [InlineData("for misty@example.test", "for [email]")]
    public void Explain_ImportFailures_UseTheirMessage(string message, string expected) =>
        Assert.Equal(expected, DailyImportJob.Explain(new ImportException(message)));

    [Fact]
    public void Explain_SavingAndUnexpectedFailures()
    {
        Assert.Equal("Saving the game data failed: duplicate key", DailyImportJob.Explain(new DbUpdateException("outer", new InvalidOperationException("duplicate key"))));
        Assert.Equal("Saving the game data failed: outer", DailyImportJob.Explain(new DbUpdateException("outer")));
        Assert.Equal("Unexpected error (InvalidOperationException): boom", DailyImportJob.Explain(new InvalidOperationException("boom")));
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private sealed class FreeLock : IRunLock, IAsyncDisposable
    {
        public bool Released { get; private set; }

        public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(this);

        public ValueTask DisposeAsync()
        {
            Released = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class TakenLock : IRunLock
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(null);
    }
}
