using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyPoGoCompanion.Ai;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Seed;
using MyPoGoCompanion.Jobs;
using MyPoGoCompanion.Jobs.Research;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class WeeklyResearcherTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ReplaysPagesAndResponsesAndPublishesOnlyValidatedCompleteRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        var connection = await postgres.CreateDatabaseAsync(ct);
        await using var db = PostgresFixture.CreateContext(connection);
        await DevSeed.SeedAsync(db, DateTimeOffset.UtcNow, ct);
        var replay = new Replay();
        async Task<int> Run() => await JobHost.RunAsync(["weekly-research", "--ConnectionStrings:Database=" + connection],
            builder =>
            {
                builder.Services.AddSingleton<IResearchSource>(replay);
                builder.Services.AddSingleton<IAiClient>(replay);
            }, ct);

        Assert.Equal(0, await Run());
        var ev = await db.Events.Include(e => e.Pokemon).SingleAsync(e => e.Key == "event/events/raid-day", ct);
        Assert.Equal("kyogre", Assert.Single(ev.Pokemon).FormId);
        Assert.Equal("https://leekduck.com/events/raid-day/", ev.SourceUrl);
        var egg = await db.EggPoolEntries.SingleAsync(e => e.Key == "egg/TenKm/kyogre", ct);
        var raid = await db.RaidBosses.SingleAsync(e => e.Key == "raid/kyogre/FiveStar/False/2026-11-29", ct);
        egg.FixedAt = DateTimeOffset.UtcNow;
        egg.Rarity = 5;
        raid.RemovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        Assert.Equal(0, await Run());
        await db.Entry(ev).ReloadAsync(ct);
        Assert.Null(ev.ChangedAt);

        replay.Changed = true;
        Assert.Equal(0, await Run());
        await db.Entry(egg).ReloadAsync(ct);
        await db.Entry(ev).ReloadAsync(ct);
        await db.Entry(raid).ReloadAsync(ct);
        Assert.Equal(5, egg.Rarity);
        Assert.NotNull(egg.SourceChangedAt);
        Assert.NotNull(raid.RemovedAt);
        Assert.False(raid.ShinyAvailable);
        Assert.Equal("Updated event", ev.NameEn);
        Assert.NotNull(ev.ChangedAt);
        Assert.True(await db.ResearchRewards.AnyAsync(r => r.Task.Key == "research/win a raid" && r.ShinyAvailable, ct));
        Assert.True(await db.MaxBosses.AnyAsync(b => b.Key.StartsWith("max/kyogre/") && b.Stars == 5, ct));

        replay.Changed = false;
        replay.Unknown = true;
        Assert.Equal(1, await Run());
        await db.Entry(ev).ReloadAsync(ct);
        Assert.Equal("Updated event", ev.NameEn);
        var last = await db.JobRuns.OrderByDescending(r => r.StartedAt).FirstAsync(ct);
        Assert.Equal(JobStatus.Failed, last.Status);
        Assert.Contains("costUsd", last.Summary, StringComparison.Ordinal);
    }

    private sealed class Replay : IResearchSource, IAiClient
    {
        public bool Changed { get; set; }
        public bool Unknown { get; set; }
        public Task<IReadOnlyList<ResearchSearchResult>> SearchAsync(ResearchTopic topic, string? titleQuery = null, CancellationToken cancellationToken = default)
        {
            var path = topic == ResearchTopic.Events ? "events/raid-day/" : topic.ToString();
            return Task.FromResult<IReadOnlyList<ResearchSearchResult>>([new(topic.ToString(), new Uri("https://leekduck.com/" + path))]);
        }

        public Task<ResearchPage> FetchAsync(Uri url, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchPage(url, $"<main>{url.AbsolutePath.Trim('/')}</main>", DateTimeOffset.UtcNow));

        public Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            Assert.NotNull(request.JobRunId);
            Assert.Equal(AiPurpose.Researcher, request.Purpose);
            var json = request.Input switch
            {
                "events/raid-day" => """[{"start":"2026-11-29T14:00","kind":"RaidDay","name":"Raid Day","encounters":[{"formId":"kyogre","shiny":true}]}]""",
                "Raids" => """[{"start":"2026-11-29T14:00","kind":"FiveStar","formId":"kyogre","shiny":false}]""",
                "MaxBattles" => """[{"start":"2026-11-29T14:00","stars":5,"formId":"kyogre"}]""",
                "Eggs" => """[{"kind":"TenKm","formId":"kyogre","rarity":1}]""",
                "FieldResearch" => """[{"name":"Win a raid","encounters":[{"formId":"kyogre","shiny":false}]}]""",
                _ => throw new InvalidOperationException(),
            };
            if (Changed)
            {
                json = json.Replace("Raid Day", "Updated event", StringComparison.Ordinal)
                    .Replace("false", "true", StringComparison.Ordinal).Replace("rarity\":1", "rarity\":2", StringComparison.Ordinal);
            }
            if (Unknown && request.Input == "FieldResearch")
            {
                json = json.Replace("kyogre", "unknown", StringComparison.Ordinal);
            }
            return Task.FromResult(new AiResult(json, "replay", 0, 0, 0, false));
        }
    }
}
