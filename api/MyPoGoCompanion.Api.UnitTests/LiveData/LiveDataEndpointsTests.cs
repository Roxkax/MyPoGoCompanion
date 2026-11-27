using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyPoGoCompanion.Api.LiveData;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Seed;

namespace MyPoGoCompanion.Api.UnitTests.LiveData;

public sealed class LiveDataEndpointsTests
{
    private static readonly DateTimeOffset _now = new(DevSeed.SampleNow, TimeSpan.Zero);
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RaidsAndMaxBattles_PublishCurrentAndUpcomingRotations()
    {
        await using var db = await SampleAsync();
        await using var app = BuildApi(db);
        var raids = await ReadAsync<RaidRotations>(app, "/raids", "?timeZone=Europe%2FLisbon");
        var mega = Assert.Single(raids.Now, boss => boss.Pokemon.FormId == "gengar_mega");
        Assert.Equal("gengar", mega.CatchFormId);
        Assert.Equal(1644, mega.PerfectCp);
        Assert.Equal(2055, mega.WeatherBoostedPerfectCp);
        Assert.Contains(raids.Now, boss => boss.Pokemon.FormId == "absol" && boss.IsShadow);
        Assert.Equal(["chandelure_mega", "groudon"], raids.Next.Select(boss => boss.Pokemon.FormId));

        var max = await ReadAsync<MaxBattleRotations>(app, "/max-battles");
        Assert.Contains(max.Now, boss => boss.Pokemon.FormId == "venusaur" && boss.IsGigantamax);
        Assert.Equal("lapras", Assert.Single(max.Next).Pokemon.FormId);
    }

    [Fact]
    public async Task Events_ReturnTheSelectedCalendarWindowWithItsDetails()
    {
        await using var db = await SampleAsync();
        await using var app = BuildApi(db);
        var today = await ReadAsync<LiveCalendarEvent[]>(app, "/events");
        Assert.Equal("Halloween event", Assert.Single(today).NameEn);

        var next = await ReadAsync<LiveCalendarEvent[]>(app, "/events", "?date=2026-11-07&days=2&timeZone=Asia%2FTokyo");
        var communityDay = Assert.Single(next);
        Assert.Equal(EventKind.CommunityDay, communityDay.Kind);
        Assert.Equal(TimeSpan.FromHours(9), communityDay.Schedule.StartsAt.Offset);
        var featured = Assert.Single(communityDay.Pokemon);
        Assert.Equal("gible", featured.Pokemon.FormId);
        Assert.True(featured.Shiny);
        Assert.Equal("EARTH_POWER", Assert.Single(communityDay.Moves).MoveId);
        Assert.Equal("3× catch Stardust", communityDay.Bonuses[0].TextEn);
    }

    [Fact]
    public async Task EggAndResearchEncounters_UseTheAvailableMultiplierForTheirLevel()
    {
        await using var db = await SampleAsync();
        var halloween = await db.Events.SingleAsync(e => e.Key == "event/halloween-2026", Ct);
        db.EggPoolEntries.Add(new EggPoolEntry
        {
            Key = "event-egg",
            Egg = EggKind.SevenKm,
            FormId = "gible",
            Rarity = 3,
            ShinyAvailable = true,
            Event = halloween,
        });
        await db.SaveChangesAsync(Ct);

        await using var app = BuildApi(db);
        var eggs = await ReadAsync<LiveEgg[]>(app, "/eggs");
        var egg = Assert.Single(eggs, row => row.Egg == EggKind.SevenKm);
        Assert.Equal((3, 635, "Halloween event"), (egg.Rarity, egg.PerfectCp, egg.Event?.NameEn));
        Assert.True(egg.ShinyAvailable);

        var tasks = await ReadAsync<LiveResearchTask[]>(app, "/research");
        var reward = Assert.Single(tasks.Single(task => task.TaskEn == "Win 3 raids").Rewards);
        Assert.Equal(("gible", 477), (reward.Pokemon.FormId, reward.PerfectCp));
        Assert.DoesNotContain(tasks, task => task.TaskEn == "Catch 15 Gible");

        db.CpMultipliers.Remove(await db.CpMultipliers.SingleAsync(m => m.Level == 15m, Ct));
        await db.SaveChangesAsync(Ct);
        var missing = await ReadAsync<LiveResearchTask[]>(app, "/research");
        Assert.All(missing.SelectMany(task => task.Rewards), item => Assert.Null(item.PerfectCp));
    }

    [Fact]
    public async Task UndatedFeed_OnlyUsesTheLastSuccessfulSnapshotAndFixedRows()
    {
        await using var db = await SampleAsync();
        var recent = _now.AddHours(-3);
        db.JobRuns.AddRange(
            new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Succeeded, StartedAt = recent, FinishedAt = recent.AddMinutes(5) },
            new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Failed, StartedAt = _now.AddHours(-1) });
        var egg = await db.EggPoolEntries.SingleAsync(row => row.FormId == "gible", Ct);
        egg.FixedAt = _now.AddDays(-1);
        var task = await db.ResearchTasks.SingleAsync(row => row.TaskEn == "Win 3 raids", Ct);
        task.FetchedAt = recent;
        await db.SaveChangesAsync(Ct);

        await using var app = BuildApi(db);
        Assert.Equal("gible", Assert.Single(await ReadAsync<LiveEgg[]>(app, "/eggs")).Pokemon.FormId);
        var research = await ReadAsync<LiveResearchTask[]>(app, "/research");
        Assert.Equal(["Catch 10 Ghost-type Pokémon", "Win 3 raids"], research.Select(row => row.TaskEn));
    }

    [Theory]
    [InlineData("/raids", "?timeZone=Mars%2FColony", "Invalid time zone")]
    [InlineData("/max-battles", "?timeZone=Mars%2FColony", "Invalid time zone")]
    [InlineData("/events", "?timeZone=Mars%2FColony", "Invalid time zone")]
    [InlineData("/events", "?days=0", "Invalid calendar range")]
    [InlineData("/eggs", "?timeZone=Mars%2FColony", "Invalid time zone")]
    [InlineData("/research", "?timeZone=Mars%2FColony", "Invalid time zone")]
    public async Task InvalidQueries_ReturnProblemDetails(string path, string query, string title)
    {
        await using var db = NewDb();
        await using var app = BuildApi(db);
        var (status, body) = await CallAsync(app, path, query);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(title, problem.RootElement.GetProperty("title").GetString());
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<AppDbContext> SampleAsync()
    {
        var db = NewDb();
        await DevSeed.SeedAsync(db, _now, Ct);
        return db;
    }

    private static WebApplication BuildApi(AppDbContext db)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton<TimeProvider>(new SampleClock());
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        var app = builder.Build();
        app.MapLiveDataEndpoints();
        return app;
    }

    private static async Task<T> ReadAsync<T>(WebApplication app, string path, string query = "")
    {
        var (status, body) = await CallAsync(app, path, query);
        Assert.Equal(StatusCodes.Status200OK, status);
        return Assert.IsType<T>(JsonSerializer.Deserialize<T>(body, _json));
    }

    private static async Task<(int Status, byte[] Json)> CallAsync(WebApplication app, string path, string query)
    {
        var route = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>(),
            endpoint => endpoint.RoutePattern.RawText == path);
        await using var scope = app.Services.CreateAsyncScope();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = Ct };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = body;
        await route.RequestDelegate!(context);
        return (context.Response.StatusCode, body.ToArray());
    }

    private sealed class SampleClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
