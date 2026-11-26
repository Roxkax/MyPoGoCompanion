using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MyPoGoCompanion.Ai;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Jobs.DailyImport;
using MyPoGoCompanion.Jobs.Research;

namespace MyPoGoCompanion.Jobs.UnitTests;

public sealed class WeeklyResearcherTests
{
    [Theory]
    [InlineData("success", 0)]
    [InlineData("budget", 1)]
    [InlineData("incomplete", 1)]
    [InlineData("unconfigured", 1)]
    [InlineData("duplicate", 1)]
    public async Task RecordsTheOutcomeAndRecoversInterruptedRuns(string response, int expectedExit)
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        await using var db = new AppDbContext(options);
        var interrupted = new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Running };
        db.Add(interrupted);
        db.Add(new AiUsage { Model = "test", JobRunId = interrupted.Id, CostUsd = 0.01m, Purpose = AiPurpose.Researcher });
        await db.SaveChangesAsync(ct);
        var fixture = new Fixture(response);
        var researcher = new WeeklyResearcher(db, fixture, fixture, TimeProvider.System, fixture, NullLogger<WeeklyResearcher>.Instance);
        Assert.Equal(expectedExit, await researcher.RunAsync(ct));
        db.ChangeTracker.Clear();
        var earlier = await db.JobRuns.SingleAsync(r => r.Id == interrupted.Id, ct);
        Assert.Equal(JobStatus.Failed, earlier.Status);
        Assert.Contains("0.01", earlier.Summary, StringComparison.Ordinal);
        var current = await db.JobRuns.SingleAsync(r => r.Id != interrupted.Id, ct);
        Assert.NotNull(current.FinishedAt);
        Assert.Equal(expectedExit == 0 ? JobStatus.Succeeded : JobStatus.Failed, current.Status);
        Assert.Equal(expectedExit == 0 ? 1 : 0, await db.Events.CountAsync(ct));
        if (response == "budget")
        {
            Assert.Contains("budget", current.Error, StringComparison.Ordinal);
        }
    }

    private sealed class Fixture(string response) : IResearchSource, IAiClient, IRunLock, IAsyncDisposable
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(this);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<IReadOnlyList<ResearchSearchResult>> SearchAsync(ResearchTopic topic, string? titleQuery = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResearchSearchResult>>(topic == ResearchTopic.Events
                ? [new("Events", new("https://leekduck.com/events/")), new("Event", new("https://leekduck.com/events/raid-day/"))]
                : []);

        public Task<ResearchPage> FetchAsync(Uri url, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchPage(url, "<main>Raid day</main>", DateTimeOffset.UnixEpoch));

        public Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            const string fact = """{"kind":"RaidDay","name":"Raid day","start":"2026-12-01T10:00"}""";
            return response switch
            {
                "budget" => throw new AiBudgetExceededException(DateTimeOffset.UtcNow.AddDays(1)),
                "unconfigured" => throw new AiUnavailableException(),
                _ => Task.FromResult(new AiResult(response == "duplicate" ? $"[{fact},{fact}]" : $"[{fact}]",
                    "test", 0, 0, 0, response == "incomplete")),
            };
        }
    }
}
