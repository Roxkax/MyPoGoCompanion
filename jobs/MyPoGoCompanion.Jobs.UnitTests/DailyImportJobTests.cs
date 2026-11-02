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
    private static readonly DailyImportOptions _options = new() { RetryDelay = TimeSpan.Zero, Trigger = "schedule" };

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
        var downloader = new SourceDownloader(new HttpClient(_http), Options.Create(options ?? _options), NullLogger<SourceDownloader>.Instance);
        var job = new DailyImportJob(db, runLock ?? new FreeLock(), downloader, Options.Create(options ?? _options), configuration, _time, NullLogger<DailyImportJob>.Instance);
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
        Assert.Equal(6, summary.Sources.Count);
        Assert.Equal(Fixtures.Bytes("GAME_MASTER.json").Length, summary.Sources["gameMaster"].Bytes);
        Assert.Equal(Fixtures.Built.Forms.Count, summary.Changes["forms"].Added);
        Assert.Equal(18, summary.Changes["types"].Added);
        Assert.Contains(summary.Warnings, warning => warning.Contains("basculegion", StringComparison.Ordinal));

        await using var db = Db();
        Assert.Equal(Fixtures.Built.Forms.Count, await db.Forms.CountAsync(Ct));
        Assert.Equal(Fixtures.Built.Evolutions.Count, await db.Evolutions.CountAsync(Ct));
        Assert.Equal(Fixtures.Built.PvpRankings.Count, await db.PvpRankings.CountAsync(Ct));
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
    public async Task ACutOffSource_IsRefused()
    {
        await using (var db = Db())
        {
            // Pretend the database already holds many more released forms than the sources list.
            db.Types.Add(new PokemonType { Id = "normal", NameEn = "Normal", NamePt = "Normal", SortOrder = 1 });
            db.Families.Add(new Family { Id = "FAMILY_X" });
            db.Species.Add(new Species { Dex = 9000, NameEn = "X", FamilyId = "FAMILY_X", Generation = 9 });
            for (var i = 0; i < 500; i++)
            {
                db.Forms.Add(new Form { Id = $"x{i}", Dex = 9000, NameEn = "X", Type1Id = "normal", Attack = 1, Defense = 1, Stamina = 1, Released = true, IsDefault = i == 0 });
            }

            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(DailyImportOutcome.Failed, await RunAsync());

        var run = Assert.Single(await RunsAsync());
        Assert.Contains("far fewer than the 500 already stored", run.Error, StringComparison.Ordinal);
        await using var check = Db();
        Assert.Equal(500, await check.Forms.CountAsync(form => form.Released, Ct));
    }

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
            charizard.NamePt = "Charizard";
            (await db.Species.SingleAsync(s => s.Dex == 6, Ct)).NamePt = "Charizard";
            (await db.Moves.SingleAsync(m => m.Id == "FIRE_SPIN", Ct)).NamePt = "Giro de Fogo";
            (await db.Evolutions.FirstAsync(Ct)).TaskPt = "Tarefa";
            await db.SaveChangesAsync(Ct);
        }

        await RunAsync(version: "def5678");

        await using var check = Db();
        var form = await check.Forms.SingleAsync(f => f.Id == "charizard", Ct);
        Assert.Equal((true, "Charizard"), (form.ShinyReleased, form.NamePt));
        Assert.Equal("Charizard", (await check.Species.SingleAsync(s => s.Dex == 6, Ct)).NamePt);
        Assert.Equal("Giro de Fogo", (await check.Moves.SingleAsync(m => m.Id == "FIRE_SPIN", Ct)).NamePt);
        Assert.Contains(await check.Evolutions.ToListAsync(Ct), e => e.TaskPt == "Tarefa");
        Assert.All(Summary((await RunsAsync())[1]).Changes.Values, kind => Assert.Equal(0, kind.Total));
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
        _http.Responses[new Uri(_options.PvpokeBaseUrl, "data/gamemaster.json").AbsoluteUri] = () =>
            new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(pvpoke, JsonSerializerOptions.Web)) };

        // Bulbasaur's only form is gone, so the share of released forms drops: allow it here.
        Assert.Equal(DailyImportOutcome.Succeeded, await RunAsync(version: "def5678", options: new DailyImportOptions { RetryDelay = TimeSpan.Zero, MinimumReleasedFormsShare = 0.5 }));

        var changes = Summary((await RunsAsync())[1]).Changes;
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
