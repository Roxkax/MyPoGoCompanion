using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.LiveData;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Seed;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class LiveDataTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 18, 30, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rotations_SplitCurrentAndUpcomingByThePlayersClock_AndCalculateTheRaidCatchCp()
    {
        var database = await SeedAsync();
        Guid boundaryId;
        await using (var db = PostgresFixture.CreateContext(database))
        {
            var boundary = Raid("boundary", Wall(1), Wall(12));
            boundaryId = boundary.Id;
            db.RaidBosses.AddRange(boundary,
                Raid("removed", _now.UtcDateTime.AddHours(-1), _now.UtcDateTime.AddHours(1), removed: true),
                Raid("ended", _now.UtcDateTime.AddHours(-2), _now.UtcDateTime));
            db.MaxBosses.Add(new MaxBoss { Key = "ended-max", FormId = "gible", Stars = 3, StartsAt = Wall(-1), EndsAt = Wall(0), TimeZone = "UTC" });
            await db.SaveChangesAsync(Ct);
        }

        await using var api = new ApiFactory(database, clock: new FixedClock(_now));
        using var client = api.CreateClient();

        var utcRaids = await GetAsync<RaidRotations>(client, "/raids");
        Assert.Contains(utcRaids.Next, boss => boss.Id == boundaryId);
        Assert.DoesNotContain(utcRaids.Now, boss => boss.Id == boundaryId);
        var lisbon = await GetAsync<RaidRotations>(client, "/raids?timeZone=Europe%2FLisbon");
        Assert.Contains(lisbon.Next, boss => boss.Id == boundaryId);
        var tokyo = await GetAsync<RaidRotations>(client, "/raids?timeZone=Asia%2FTokyo");
        Assert.Contains(tokyo.Now, boss => boss.Id == boundaryId && boss.Schedule.StartsAt.Offset == TimeSpan.FromHours(9));

        Assert.Equal(7, utcRaids.Now.Count);
        var kyogre = Assert.Single(utcRaids.Now, boss => boss.Pokemon.FormId == "kyogre");
        Assert.Equal((RaidTier.FiveStar, true, "kyogre", 2351, 2939),
            (kyogre.Tier, kyogre.ShinyAvailable, kyogre.CatchFormId, kyogre.PerfectCp, kyogre.WeatherBoostedPerfectCp));
        var gengar = Assert.Single(utcRaids.Now, boss => boss.Pokemon.FormId == "gengar_mega");
        Assert.Equal(("Mega Gengar", "gengar", 1644, 2055, "Halloween event"),
            (gengar.Pokemon.NameEn, gengar.CatchFormId, gengar.PerfectCp, gengar.WeatherBoostedPerfectCp, gengar.Event?.NameEn));
        Assert.Contains(utcRaids.Now, boss => boss.Pokemon.FormId == "absol" && boss.IsShadow);
        Assert.Equal(["gible", "chandelure_mega", "groudon"], utcRaids.Next.Select(boss => boss.Pokemon.FormId));

        var max = await GetAsync<MaxBattleRotations>(client, "/max-battles");
        Assert.Equal(2, max.Now.Count);
        Assert.Contains(max.Now, boss => boss.Pokemon.FormId == "venusaur" && boss.Stars == 6 && boss.IsGigantamax);
        Assert.Equal("lapras", Assert.Single(max.Next).Pokemon.FormId);
    }

    [Fact]
    public async Task Calendar_UsesLocalDaysAndGlobalInstants_AndIncludesFeaturedPokemonBonusesAndMoves()
    {
        var database = await SeedAsync();
        await using (var db = PostgresFixture.CreateContext(database))
        {
            db.Events.AddRange(
                Event("global", "Global event", new DateTime(2026, 11, 7, 1, 0, 0), new DateTime(2026, 11, 7, 2, 0, 0), "Pacific/Kiritimati"),
                Event("open", "Starts Saturday", new DateTime(2026, 11, 7, 12, 0, 0), null),
                Event("midnight", "Ends at midnight", new DateTime(2026, 11, 6, 23, 0, 0), new DateTime(2026, 11, 7, 0, 0, 0)),
                Event("removed", "Removed", new DateTime(2026, 11, 7, 12, 0, 0), null, removed: true));
            await db.SaveChangesAsync(Ct);
        }

        await using var api = new ApiFactory(database, clock: new FixedClock(_now));
        using var client = api.CreateClient();
        var dates = await GetAsync<LiveCalendarEvent[]>(client, "/events?date=2026-11-06&days=2&timeZone=Pacific%2FHonolulu");
        Assert.Equal(["Global event", "Ends at midnight", "Starts Saturday", "Community Day: Gible"], dates.Select(e => e.NameEn));
        Assert.Equal(new DateTime(2026, 11, 6, 1, 0, 0), dates[0].Schedule.StartsAt.DateTime);

        var saturday = await GetAsync<LiveCalendarEvent[]>(client, "/events?date=2026-11-07");
        Assert.Equal(["Starts Saturday", "Community Day: Gible"], saturday.Select(e => e.NameEn));
        var communityDay = saturday[1];
        Assert.Equal(EventKind.CommunityDay, communityDay.Kind);
        Assert.True(Assert.Single(communityDay.Pokemon).Shiny);
        var move = Assert.Single(communityDay.Moves);
        Assert.Equal(("garchomp", "EARTH_POWER", "Evolve Gabite 14:00–22:00"), (move.Pokemon.FormId, move.MoveId, move.HowEn));
        Assert.Equal("3× catch Stardust", communityDay.Bonuses[0].TextEn);
        Assert.Empty(await GetAsync<LiveCalendarEvent[]>(client, "/events?date=2026-11-08"));
        Assert.Equal("Halloween event", Assert.Single(await GetAsync<LiveCalendarEvent[]>(client, "/events")).NameEn);

        var response = await client.GetStringAsync(new Uri("/events?date=2026-11-07", UriKind.Relative), Ct);
        Assert.DoesNotContain("leekduck.com", response, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceSnapshot", response, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LatestSuccessfulSnapshot_KeepsDatesAndAdminFixes_AndFiltersSupersededEntries()
    {
        var database = await SeedAsync();
        await using (var db = PostgresFixture.CreateContext(database))
        {
            var active = await db.Events.SingleAsync(e => e.Key == "event/halloween-2026", Ct);
            var future = await db.Events.SingleAsync(e => e.Key == "event/community-day-gible-2026-11", Ct);
            var removed = Event("removed", "Hidden event", Wall(-3), Wall(3), removed: true);
            var ended = Event("ended", "Finished event", Wall(-3), Wall(-1));
            var old = _now.AddDays(-2);
            var seen = _now.AddHours(-4);
            db.JobRuns.AddRange(
                new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Succeeded, StartedAt = seen, FinishedAt = seen.AddMinutes(5) },
                new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Failed, StartedAt = _now.AddHours(-2), FinishedAt = _now.AddHours(-1) });
            db.EggPoolEntries.AddRange(
                Egg("seen", seen), Egg("old", old), Egg("fixed", old, fixedAt: old), Egg("removed", seen, removedAt: seen),
                Egg("event-active", old, active), Egg("event-future", seen, future), Egg("event-removed", seen, removed), Egg("event-ended", seen, ended));
            db.ResearchTasks.AddRange(
                Research("Seen", seen), Research("Stale", old), Research("Fixed", old, fixedAt: old), Research("Removed", seen, removedAt: seen),
                Research("Event removed", seen, removed), Research("Ended", seen, ended));
            db.RaidBosses.AddRange(
                Raid("current-no-end", Wall(-3), null, seen), Raid("old-no-end", Wall(-3), null, old));
            db.MaxBosses.AddRange(
                new MaxBoss { Key = "current-no-end", FormId = "gible", Stars = 3, StartsAt = Wall(-3), FetchedAt = seen },
                new MaxBoss { Key = "old-no-end", FormId = "gible", Stars = 4, StartsAt = Wall(-3), FetchedAt = old });
            await db.SaveChangesAsync(Ct);
        }

        await using var api = new ApiFactory(database, clock: new FixedClock(_now));
        using var client = api.CreateClient();

        var eggs = await GetAsync<LiveEgg[]>(client, "/eggs");
        Assert.Equal(3, eggs.Length);
        Assert.All(eggs, e => Assert.Equal((EggKind.SevenKm, "gible", 635), (e.Egg, e.Pokemon.FormId, e.PerfectCp)));
        Assert.Equal("Halloween event", Assert.Single(eggs, e => e.Event is not null).Event!.NameEn);
        var research = await GetAsync<LiveResearchTask[]>(client, "/research");
        Assert.Equal(["Catch 10 Ghost-type Pokémon", "Fixed", "Seen"], research.Select(t => t.TaskEn));
        Assert.Equal(477, Assert.Single(research.Single(task => task.TaskEn == "Seen").Rewards).PerfectCp);

        var raids = await GetAsync<RaidRotations>(client, "/raids");
        Assert.Single(raids.Now, boss => boss.Schedule.EndsAt == null);
        var maxes = await GetAsync<MaxBattleRotations>(client, "/max-battles");
        Assert.Equal(3, Assert.Single(maxes.Now, boss => boss.Schedule.EndsAt == null).Stars);

        await using var check = PostgresFixture.CreateContext(database);
        Assert.True(await check.EggPoolEntries.AnyAsync(e => e.Key == "old", Ct));
        Assert.True(await check.ResearchTasks.AnyAsync(t => t.Key == "Stale", Ct));
        Assert.True(await check.RaidBosses.AnyAsync(r => r.Key == "old-no-end", Ct));
    }

    [Theory]
    [InlineData("/raids?timeZone=Mars%2FColony")]
    [InlineData("/max-battles?timeZone=Mars%2FColony")]
    [InlineData("/events?date=2026-02-30")]
    [InlineData("/events?days=32")]
    [InlineData("/events?date=9999-12-31&days=2")]
    [InlineData("/eggs?timeZone=Mars%2FColony")]
    [InlineData("/research?timeZone=Mars%2FColony")]
    public async Task InvalidParameters_ReturnProblemDetails(string path)
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsEmptyFeeds()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();
        var raids = await GetAsync<RaidRotations>(client, "/raids");
        Assert.Empty(raids.Now);
        Assert.Empty(raids.Next);
        var max = await GetAsync<MaxBattleRotations>(client, "/max-battles");
        Assert.Empty(max.Now);
        Assert.Empty(max.Next);
        Assert.Empty(await GetAsync<LiveCalendarEvent[]>(client, "/events"));
        Assert.Empty(await GetAsync<LiveEgg[]>(client, "/eggs"));
        Assert.Empty(await GetAsync<LiveResearchTask[]>(client, "/research"));
    }

    private async Task<string> SeedAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using var db = PostgresFixture.CreateContext(database);
        await DevSeed.SeedAsync(db, _now, Ct);
        return database;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string path) =>
        (await client.GetFromJsonAsync<T>(new Uri(path, UriKind.Relative), _json, Ct))!;

    private static DateTime Wall(int hours) => DateTime.SpecifyKind(_now.UtcDateTime.AddHours(hours), DateTimeKind.Unspecified);

    private static RaidBoss Raid(string key, DateTime start, DateTime? end, DateTimeOffset? fetched = null, bool removed = false) =>
        new()
        {
            Key = key,
            FormId = "gible",
            Tier = RaidTier.OneStar,
            StartsAt = DateTime.SpecifyKind(start, DateTimeKind.Unspecified),
            EndsAt = end is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Unspecified) : null,
            FetchedAt = fetched ?? _now,
            RemovedAt = removed ? _now : null,
        };

    private static GameEvent Event(string key, string name, DateTime start, DateTime? end, string? zone = null, bool removed = false) =>
        new() { Key = key, NameEn = name, StartsAt = start, EndsAt = end, TimeZone = zone, RemovedAt = removed ? _now : null };

    private static EggPoolEntry Egg(string key, DateTimeOffset seen, GameEvent? gameEvent = null, DateTimeOffset? fixedAt = null, DateTimeOffset? removedAt = null) =>
        new() { Key = key, Egg = EggKind.SevenKm, FormId = "gible", Event = gameEvent, FetchedAt = seen, FixedAt = fixedAt, RemovedAt = removedAt };

    private static ResearchTask Research(string key, DateTimeOffset seen, GameEvent? gameEvent = null, DateTimeOffset? fixedAt = null, DateTimeOffset? removedAt = null)
    {
        var task = new ResearchTask { Key = key, TaskEn = key, Event = gameEvent, FetchedAt = seen, FixedAt = fixedAt, RemovedAt = removedAt };
        task.Rewards.Add(new ResearchReward { FormId = "gible" });
        return task;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
