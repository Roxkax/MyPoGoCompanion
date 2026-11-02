using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyPoGoCompanion.Api.Freshness;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>The out-of-date warning's source: the last successful run of each job, read from a real database.</summary>
public sealed class FreshnessTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerOptions.Web);

    private static JobRun Run(JobKind job, JobStatus status, DateTimeOffset finishedAt) => new()
    {
        Job = job,
        Status = status,
        StartedAt = finishedAt.AddMinutes(-5),
        FinishedAt = status == JobStatus.Running ? null : finishedAt,
    };

    private async Task<(HttpResponseMessage Response, FreshnessResponse Body)> GetAsync(string database, Dictionary<string, string?>? settings = null)
    {
        await using var api = new ApiFactory(database, settings: settings);
        using var client = api.CreateClient();
        var response = await client.GetAsync(new Uri("/freshness", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (response, (await response.Content.ReadFromJsonAsync<FreshnessResponse>(_json, Ct))!);
    }

    [Fact]
    public async Task NoRunsYet_NothingIsOutOfDate()
    {
        var (response, body) = await GetAsync(await postgres.CreateDatabaseAsync(Ct));

        Assert.Equal(new DataFreshness(null, null, false, 3), body.GameData);
        Assert.Equal(new DataFreshness(null, null, false, 9), body.LiveData);
        Assert.Equal("public, max-age=300", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task LastSuccessfulRunOfEachJob_DecidesItsKind()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        var now = DateTimeOffset.UtcNow;
        await using (var db = PostgresFixture.CreateContext(database))
        {
            db.JobRuns.AddRange(
                // Game data: last success 4 days ago; the failures and the run in progress since then don't count.
                Run(JobKind.DailyImport, JobStatus.Succeeded, now.AddDays(-6)),
                Run(JobKind.DailyImport, JobStatus.Succeeded, now.AddDays(-4).AddHours(-1)),
                Run(JobKind.DailyImport, JobStatus.Failed, now.AddDays(-2)),
                Run(JobKind.DailyImport, JobStatus.Failed, now.AddDays(-1)),
                Run(JobKind.DailyImport, JobStatus.Running, now),
                // Raids and events: a successful run 8 days ago, inside the 9-day limit.
                Run(JobKind.AiResearcher, JobStatus.Succeeded, now.AddDays(-8).AddHours(-2)),
                // Backups say nothing about the data.
                Run(JobKind.Backup, JobStatus.Succeeded, now.AddDays(-30)));
            await db.SaveChangesAsync(Ct);
        }

        var (_, body) = await GetAsync(database);

        Assert.True(body.GameData.OutOfDate);
        Assert.Equal(4, body.GameData.DaysSinceUpdate);
        Assert.False(body.LiveData.OutOfDate);
        Assert.Equal(8, body.LiveData.DaysSinceUpdate);
    }

    [Fact]
    public async Task Limits_ComeFromConfiguration()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using (var db = PostgresFixture.CreateContext(database))
        {
            db.JobRuns.Add(Run(JobKind.AiResearcher, JobStatus.Succeeded, DateTimeOffset.UtcNow.AddDays(-2).AddHours(-1)));
            await db.SaveChangesAsync(Ct);
        }

        var (_, body) = await GetAsync(database, new() { ["Freshness:LiveDataDays"] = "2" });

        Assert.Equal(2, body.LiveData.LimitDays);
        Assert.True(body.LiveData.OutOfDate);
    }
}
