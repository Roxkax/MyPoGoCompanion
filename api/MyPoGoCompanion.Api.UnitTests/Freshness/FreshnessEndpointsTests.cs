using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Freshness;
using MyPoGoCompanion.Api.UnitTests.Auth;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Api.UnitTests.Freshness;

public class FreshnessEndpointsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static JobRun Run(JobKind job, JobStatus status, DateTimeOffset finishedAt) => new()
    {
        Job = job,
        Status = status,
        StartedAt = finishedAt.AddMinutes(-4),
        FinishedAt = status == JobStatus.Running ? null : finishedAt,
    };

    private static async Task<(FreshnessResponse Body, HttpContext Http)> GetAsync(AppDbContext db, FreshnessOptions? options = null)
    {
        var http = new DefaultHttpContext();
        var body = await FreshnessEndpoints.GetFreshnessAsync(
            db, Options.Create(options ?? new FreshnessOptions()), new TestClock(_now), http, Ct);
        return (body, http);
    }

    [Fact]
    public void Describe_NeverRun_IsNotOutOfDate()
    {
        Assert.Equal(new DataFreshness(null, null, false, 3), FreshnessEndpoints.Describe(null, 3, _now));
    }

    [Theory]
    [InlineData(0, 0, false)] // just now
    [InlineData(2 * 24 + 23, 2, false)] // 2 days 23 hours: still inside the 3-day limit
    [InlineData(3 * 24, 3, true)] // exactly 3 days: the warning starts
    [InlineData(4 * 24 + 5, 4, true)]
    public void Describe_CountsWholeDays_AndWarnsFromTheLimit(int hoursAgo, int days, bool outOfDate)
    {
        var last = _now.AddHours(-hoursAgo);

        Assert.Equal(new DataFreshness(last, days, outOfDate, 3), FreshnessEndpoints.Describe(last, 3, _now));
    }

    [Fact]
    public void Describe_RunFinishedAfterNow_CountsAsZeroDays()
    {
        // A clock a little ahead on the job's side mustn't give "-1 days".
        Assert.Equal(0, FreshnessEndpoints.Describe(_now.AddMinutes(3), 3, _now).DaysSinceUpdate);
    }

    [Fact]
    public async Task Get_NoRuns_NothingIsOutOfDate_AndTheAnswerMayBeCached()
    {
        using var db = NewDb();

        var (body, http) = await GetAsync(db);

        Assert.Equal(new FreshnessResponse(new DataFreshness(null, null, false, 3), new DataFreshness(null, null, false, 9)), body);
        Assert.Equal("public, max-age=300", http.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Get_UsesTheLastSuccessfulRunOfEachJob()
    {
        using var db = NewDb();
        db.JobRuns.AddRange(
            Run(JobKind.DailyImport, JobStatus.Succeeded, _now.AddDays(-5)),
            Run(JobKind.DailyImport, JobStatus.Succeeded, _now.AddDays(-3).AddHours(-2)),
            Run(JobKind.DailyImport, JobStatus.Failed, _now.AddDays(-1)),
            Run(JobKind.DailyImport, JobStatus.Running, _now),
            Run(JobKind.AiResearcher, JobStatus.Failed, _now.AddDays(-1)),
            Run(JobKind.AiResearcher, JobStatus.Succeeded, _now.AddDays(-12)),
            Run(JobKind.Backup, JobStatus.Succeeded, _now));
        await db.SaveChangesAsync(Ct);

        var (body, _) = await GetAsync(db);

        Assert.Equal(new DataFreshness(_now.AddDays(-3).AddHours(-2), 3, true, 3), body.GameData);
        Assert.Equal(new DataFreshness(_now.AddDays(-12), 12, true, 9), body.LiveData);
    }

    [Fact]
    public async Task Get_RecentRuns_AreUpToDate()
    {
        using var db = NewDb();
        db.JobRuns.AddRange(
            Run(JobKind.DailyImport, JobStatus.Succeeded, _now.AddHours(-6)),
            Run(JobKind.AiResearcher, JobStatus.Succeeded, _now.AddDays(-6)));
        await db.SaveChangesAsync(Ct);

        var (body, _) = await GetAsync(db);

        Assert.False(body.GameData.OutOfDate);
        Assert.Equal(0, body.GameData.DaysSinceUpdate);
        Assert.False(body.LiveData.OutOfDate);
        Assert.Equal(6, body.LiveData.DaysSinceUpdate);
    }

    [Fact]
    public async Task Get_UsesTheConfiguredLimits()
    {
        using var db = NewDb();
        db.JobRuns.Add(Run(JobKind.DailyImport, JobStatus.Succeeded, _now.AddDays(-1)));
        await db.SaveChangesAsync(Ct);

        var (body, _) = await GetAsync(db, new FreshnessOptions { GameDataDays = 1, LiveDataDays = 20 });

        Assert.True(body.GameData.OutOfDate);
        Assert.Equal(1, body.GameData.LimitDays);
        Assert.Equal(20, body.LiveData.LimitDays);
    }

    private static ServiceProvider Services(params (string Key, string? Value)[] values) =>
        new ServiceCollection()
            .AddFreshness(new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
                .Build())
            .BuildServiceProvider();

    [Fact]
    public void AddFreshness_Defaults_Are3And9Days()
    {
        using var services = Services();

        var options = services.GetRequiredService<IOptions<FreshnessOptions>>().Value;
        Assert.Equal(3, options.GameDataDays);
        Assert.Equal(9, options.LiveDataDays);
        Assert.Same(TimeProvider.System, services.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddFreshness_ReadsTheFreshnessSection()
    {
        using var services = Services(("Freshness:GameDataDays", "5"), ("Freshness:LiveDataDays", "14"));

        var options = services.GetRequiredService<IOptions<FreshnessOptions>>().Value;
        Assert.Equal(5, options.GameDataDays);
        Assert.Equal(14, options.LiveDataDays);
    }

    [Theory]
    [InlineData("Freshness:GameDataDays")]
    [InlineData("Freshness:LiveDataDays")]
    public void AddFreshness_ZeroDays_IsRefused(string key)
    {
        using var services = Services((key, "0"));

        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<FreshnessOptions>>().Value);
    }
}
