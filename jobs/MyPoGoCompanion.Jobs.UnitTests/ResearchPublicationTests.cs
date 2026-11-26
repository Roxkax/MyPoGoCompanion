using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Jobs.Research;

namespace MyPoGoCompanion.Jobs.UnitTests;

public sealed class ResearchPublicationTests
{
    private static readonly Uri _source = new("https://leekduck.com/events/max-monday/");

    [Theory]
    [InlineData(ResearchTopic.Raids, nameof(RaidTier.FiveStar))]
    [InlineData(ResearchTopic.MaxBattles, null)]
    [InlineData(ResearchTopic.Events, nameof(EventKind.MaxBattle))]
    [InlineData(ResearchTopic.Eggs, nameof(EggKind.TenKm))]
    [InlineData(ResearchTopic.FieldResearch, null)]
    public async Task CountsChangesAndKeepsAdminCorrections(ResearchTopic topic, string? kind)
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        var publication = new ResearchPublication(db);
        var fact = new ResearchFact
        {
            FormId = "kyogre", Kind = kind, Name = "Monday", Stars = 5, Start = new DateTime(2026, 11, 30),
            Encounters = [new("kyogre", false)],
        };
        var item = new Observation(topic, "replay", fact, _source, DateTimeOffset.UnixEpoch);
        await Apply(item);
        await Apply(item);
        Assert.Equal((1, 0, 0), (publication.Added, publication.Changed, publication.Flagged));
        var updated = item with { Fact = fact with { Shiny = true, Encounters = [new("kyogre", true)] }, At = item.At.AddDays(7) };
        await Apply(updated);
        var entry = Assert.Single(db.ChangeTracker.Entries<FetchedEntry>()).Entity;
        Assert.Equal(updated.At, entry.ChangedAt);
        entry.FixedAt = updated.At;
        await db.SaveChangesAsync(ct);
        await Apply(item with { At = updated.At.AddDays(7) });
        Assert.Equal((1, 1, 1), (publication.Added, publication.Changed, publication.Flagged));
        entry = Assert.Single(db.ChangeTracker.Entries<FetchedEntry>()).Entity;
        Assert.Equal(entry.FetchedAt, entry.SourceChangedAt);
        Assert.Equal(updated.At, entry.ChangedAt);

        async Task Apply(Observation observation)
        {
            db.ChangeTracker.Clear();
            await publication.ApplyAsync(observation, ct);
            await db.SaveChangesAsync(ct);
        }
    }
}
