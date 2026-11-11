using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Data.Seed;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>The Pokémon page's data (E03-F02), read from a real database holding the development sample.</summary>
public sealed class PokemonPageTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };

    private async Task<string> SeededDatabaseAsync()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using var db = PostgresFixture.CreateContext(database);
        await DevSeed.SeedAsync(db, DateTimeOffset.UtcNow, Ct);
        return database;
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, int dex, string? etag = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/pokedex/{dex}", UriKind.Relative));
        if (etag is not null)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        return await client.SendAsync(request, Ct);
    }

    private static async Task<PokemonPageResponse> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await JsonSerializer.DeserializeAsync<PokemonPageResponse>(await response.Content.ReadAsStreamAsync(Ct), _json, Ct))!;
    }

    [Fact]
    public async Task Garchomp_HasItsVersions_Numbers_Rankings_AndFamily()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        var page = await ReadAsync(await GetAsync(client, 445));

        Assert.Equal(("Garchomp", "FAMILY_GIBLE"), (page.NameEn, page.FamilyId));
        var garchomp = page.Forms[0];
        Assert.Equal(("garchomp", true), (garchomp.Id, garchomp.IsDefault));
        Assert.Contains(page.Forms, form => form.Kind == FormKind.Mega);
        Assert.Equal([40m, 50m, 51m], garchomp.MaxCp.Select(row => row.Level));
        Assert.Equal([15m, 20m, 25m, 30m, 35m], garchomp.PerfectCp.Select(row => row.Level));
        Assert.Equal(2.56, garchomp.Matchups.Single(m => m.Type == "ice").Multiplier, 2);
        Assert.Equal(18, garchomp.Matchups.Count);

        await using var db = PostgresFixture.CreateContext(database);
        Assert.Equal(await db.FormMoves.CountAsync(fm => fm.FormId == "garchomp", Ct), garchomp.Movepool.Count);
        Assert.Equal(await db.PvpBestIvs.CountAsync(b => b.FormId == "garchomp", Ct), garchomp.BestIvs.Count);
        Assert.Equal(await db.PvpRankings.CountAsync(r => r.FormId == "garchomp", Ct), garchomp.Pvp.Count);
        Assert.All(garchomp.Pvp, ranking => Assert.NotEmpty(ranking.Usage));
        Assert.Equal(await db.RaidRankings.CountAsync(r => r.FormId == "garchomp", Ct), garchomp.RaidRanks.Count);
        Assert.Equal(2, page.KeeperBoxes.Count);

        // Every move the page names is in its move list.
        var named = garchomp.Movepool.Select(m => m.MoveId)
            .Concat(page.Forms.SelectMany(f => f.Pvp).SelectMany(r => r.Usage.Select(u => u.MoveId).Append(r.FastMoveId).Append(r.ChargedMove1Id)))
            .ToHashSet();
        Assert.Subset(page.Moves.Select(m => m.Id).ToHashSet(), named);

        // The family: Gible → Gabite → Garchomp, no Megas.
        Assert.Equal([443, 444, 445], page.Family.Where(m => m.IsDefault).Select(m => m.Dex));
        Assert.DoesNotContain(page.Family, member => member.Kind == FormKind.Mega);
        Assert.Contains(page.Evolutions, e => (e.FromFormId, e.ToFormId) == ("gible", "gabite"));
        Assert.Contains(page.Evolutions, e => (e.FromFormId, e.ToFormId) == ("gabite", "garchomp"));
        Assert.Equal(await db.CpMultipliers.CountAsync(Ct), page.CpMultipliers.Count);
    }

    [Fact]
    public async Task Appearances_ComeFromTheLiveData_WithoutRemovedEntries()
    {
        var database = await SeededDatabaseAsync();
        await using (var db = PostgresFixture.CreateContext(database))
        {
            var egg = await db.EggPoolEntries.FirstAsync(e => e.FormId == "gible", Ct);
            egg.RemovedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(Ct);
        }

        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();
        var gible = await ReadAsync(await GetAsync(client, 443));

        Assert.Contains(gible.Appearances, a => a.Kind == AppearanceKind.Event && a.EventKind == EventKind.CommunityDay && a.StartsAt is not null);
        Assert.DoesNotContain(gible.Appearances, a => a.Kind == AppearanceKind.Egg);
        Assert.All(gible.Appearances, a => Assert.StartsWith("gible", a.FormId, StringComparison.Ordinal));

        var charizard = await ReadAsync(await GetAsync(client, 6));
        Assert.Equal(["pm6.cJAN_2020_NOEVOLVE", "pm6.cSPRING_2020_NOEVOLVE"], charizard.Costumes.Select(c => c.Sprite));
    }

    [Fact]
    public async Task AnUnknownNumber_IsNotFound()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        using var response = await GetAsync(client, 9999);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ThePage_IsUnchanged_UntilGameOrLiveDataIsPublished()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        using var first = await GetAsync(client, 445);
        Assert.Equal("public, max-age=3600", first.Headers.CacheControl?.ToString());
        var etag = first.Headers.ETag!.ToString();
        using (var again = await GetAsync(client, 445, etag))
        {
            Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
        }

        // A new raid for Garchomp, published by an AI researcher run, brings a new answer.
        await using (var db = PostgresFixture.CreateContext(database))
        {
            db.RaidBosses.Add(new RaidBoss
            {
                Key = "raid/garchomp-test",
                FormId = "garchomp",
                Tier = RaidTier.ThreeStar,
                StartsAt = new DateTime(2026, 12, 1, 10, 0, 0),
                FirstFetchedAt = DateTimeOffset.UtcNow,
                FetchedAt = DateTimeOffset.UtcNow,
            });
            var finished = DateTimeOffset.UtcNow.AddDays(1);
            db.JobRuns.Add(new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Succeeded, StartedAt = finished.AddMinutes(-1), FinishedAt = finished });
            await db.SaveChangesAsync(Ct);
        }

        using var changed = await GetAsync(client, 445, etag);
        var page = await ReadAsync(changed);
        Assert.NotEqual(etag, changed.Headers.ETag!.ToString());
        Assert.Contains(page.Appearances, a => a.Kind == AppearanceKind.Raid && a.RaidTier == RaidTier.ThreeStar);
    }

    [Fact]
    public async Task MoveUsage_FromTheImport_IsSplitByRanking()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        var page = await ReadAsync(await GetAsync(client, 445));
        var great = page.Forms[0].Pvp.FirstOrDefault(r => r.League == League.Great && !r.IsShadow);
        Assert.NotNull(great);
        Assert.Equal(great.Usage.OrderByDescending(u => u.Uses).Select(u => u.Uses), great.Usage.Select(u => u.Uses));
        Assert.Contains(great.Usage, u => u.MoveId == great.FastMoveId);
    }
}
