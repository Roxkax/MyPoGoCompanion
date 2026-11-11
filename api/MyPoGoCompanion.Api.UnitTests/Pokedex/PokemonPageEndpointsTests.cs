using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Rankings;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace MyPoGoCompanion.Api.UnitTests.Pokedex;

/// <summary>The Pokémon page's data (E03-F02).</summary>
public sealed class PokemonPageEndpointsTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions _read = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly DateTime _now = new(2026, 10, 27, 18, 30, 0);

    private readonly PokemonPageCache _cache = new();

    public void Dispose() => _cache.Dispose();

    private static Form NewForm(string id, int dex, FormKind kind, string name, int attack, string type1 = "fire", string? type2 = null) => new()
    {
        Id = id,
        Dex = dex,
        Kind = kind,
        IsDefault = kind == FormKind.Base,
        NameEn = name,
        Type1Id = type1,
        Type2Id = type2,
        Attack = attack,
        Defense = 150,
        Stamina = 150,
        Released = true,
    };

    private static T Fetched<T>(T entry)
        where T : FetchedEntry
    {
        entry.FirstFetchedAt = DateTimeOffset.UtcNow;
        entry.FetchedAt = DateTimeOffset.UtcNow;
        return entry;
    }

    /// <summary>Charmander → Charmeleon → Charizard (with a Mega and a looks-only form), and its live data.</summary>
    private static async Task<AppDbContext> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Families.Add(new Family { Id = "FAMILY_CHARMANDER" });
        db.Species.AddRange(
            new Species { Dex = 4, NameEn = "Charmander", Generation = 1, FamilyId = "FAMILY_CHARMANDER" },
            new Species { Dex = 5, NameEn = "Charmeleon", Generation = 1, FamilyId = "FAMILY_CHARMANDER" },
            new Species { Dex = 6, NameEn = "Charizard", NamePt = "Charizard", Generation = 1, FamilyId = "FAMILY_CHARMANDER" });
        var charizard = NewForm("charizard", 6, FormKind.Base, "Charizard", 223, type2: "flying");
        charizard.ShadowReleased = true;
        charizard.CanGigantamax = true;
        charizard.SpriteGigantamax = "pm6.fGIGANTAMAX";
        var mega = NewForm("charizard_mega_y", 6, FormKind.Mega, "Charizard (Mega Y)", 319, type2: "flying");
        mega.SpriteGigantamax = "left over";
        db.Forms.AddRange(
            NewForm("charmander", 4, FormKind.Base, "Charmander", 116),
            NewForm("charmeleon", 5, FormKind.Base, "Charmeleon", 158),
            charizard,
            mega,
            NewForm("charizard_costume", 6, FormKind.Alternate, "Charizard (Costume)", 223, type2: "flying"));
        db.CpMultipliers.AddRange(Enumerable.Range(0, 101).Select(i => new CpMultiplier { Level = 1 + (i / 2m), Value = 0.094 + (i * 0.0075) }));
        db.Moves.AddRange(
            new Move { Id = "FIRE_SPIN", Kind = MoveKind.Fast, TypeId = "fire", NameEn = "Fire Spin", RaidPower = 14 },
            new Move { Id = "EMBER", Kind = MoveKind.Fast, TypeId = "fire", NameEn = "Ember" },
            new Move { Id = "BLAST_BURN", Kind = MoveKind.Charged, TypeId = "fire", NameEn = "Blast Burn" },
            new Move { Id = "DRAGON_CLAW", Kind = MoveKind.Charged, TypeId = "dragon", NameEn = "Dragon Claw" },
            new Move { Id = "UNUSED", Kind = MoveKind.Charged, TypeId = "normal", NameEn = "Unused" });
        db.FormMoves.AddRange(
            new FormMove { FormId = "charizard", MoveId = "FIRE_SPIN" },
            new FormMove { FormId = "charizard", MoveId = "BLAST_BURN", Availability = MoveAvailability.EliteTm },
            new FormMove { FormId = "charmander", MoveId = "EMBER" });
        db.PvpRankings.Add(new PvpRanking { League = League.Great, FormId = "charizard", Rank = 40, Score = 80, FastMoveId = "FIRE_SPIN", ChargedMove1Id = "BLAST_BURN" });
        db.PvpMoveUsage.AddRange(
            new PvpMoveUsage { League = League.Great, FormId = "charizard", MoveId = "FIRE_SPIN", Uses = 500 },
            new PvpMoveUsage { League = League.Great, FormId = "charizard", MoveId = "DRAGON_CLAW", Uses = 900 },
            new PvpMoveUsage { League = League.Great, FormId = "charizard", IsShadow = true, MoveId = "EMBER", Uses = 1 });
        db.PvpBestIvs.AddRange(
            new PvpBestIvs { League = League.Master, FormId = "charizard", AttackIv = 15, DefenseIv = 15, StaminaIv = 15, Level = 50, Cp = 3266 },
            new PvpBestIvs { League = League.Great, FormId = "charizard_mega_y", AttackIv = 0, DefenseIv = 14, StaminaIv = 15, Level = 12, Cp = 1499 });
        db.PvpKeeperBoxes.Add(new PvpKeeperBox { League = League.Great, Dex = 6, AttackBarMax = 1, DefenseBarMin = 3, DefenseBarMax = 4, StaminaBarMin = 2, StaminaBarMax = 4 });
        db.RaidRankings.Add(new RaidRanking { AttackingTypeId = "fire", FormId = "charizard", Rank = 20, FastMoveId = "FIRE_SPIN", ChargedMoveId = "BLAST_BURN" });
        db.RaidMovesets.AddRange(
            new RaidMoveset { FormId = "charizard", FastMoveId = "FIRE_SPIN", ChargedMoveId = "BLAST_BURN", Rank = 1, Dps = 15 },
            new RaidMoveset { FormId = "charizard", IsShadow = true, FastMoveId = "FIRE_SPIN", ChargedMoveId = "BLAST_BURN", Rank = 1, Dps = 18 });
        db.Evolutions.AddRange(
            new Evolution { FromFormId = "charmander", ToFormId = "charmeleon", Candy = 25 },
            new Evolution { FromFormId = "charmeleon", ToFormId = "charizard", Candy = 100 });
        db.Costumes.Add(new Costume { Sprite = "pm6.cJAN_2020_NOEVOLVE", Dex = 6, Code = "JAN_2020_NOEVOLVE", NameEn = "January 2020" });

        var communityDay = Fetched(new GameEvent { Key = "e1", Kind = EventKind.CommunityDay, NameEn = "Community Day", StartsAt = _now.AddDays(-400), EndsAt = _now.AddDays(-400).AddHours(3) });
        communityDay.Pokemon.Add(new EventPokemon { FormId = "charizard", Shiny = true });
        var removedEvent = Fetched(new GameEvent { Key = "e2", Kind = EventKind.Special, NameEn = "Removed", StartsAt = _now, RemovedAt = DateTimeOffset.UtcNow });
        removedEvent.Pokemon.Add(new EventPokemon { FormId = "charizard" });
        db.Events.AddRange(communityDay, removedEvent);
        db.RaidBosses.AddRange(
            Fetched(new RaidBoss { Key = "r1", FormId = "charizard_mega_y", Tier = RaidTier.Mega, StartsAt = _now.AddDays(-1), EndsAt = _now.AddDays(6), Event = removedEvent }),
            Fetched(new RaidBoss { Key = "r2", FormId = "charizard", Tier = RaidTier.ThreeStar, StartsAt = _now, RemovedAt = DateTimeOffset.UtcNow }));
        db.MaxBosses.Add(Fetched(new MaxBoss { Key = "m1", FormId = "charizard", Stars = 6, IsGigantamax = true, StartsAt = _now.AddDays(-30), EndsAt = _now.AddDays(-23) }));
        db.EggPoolEntries.Add(Fetched(new EggPoolEntry { Key = "g1", FormId = "charmander", Egg = EggKind.TwoKm }));
        var task = Fetched(new ResearchTask { Key = "t1", TaskEn = "Catch 5 Fire-type Pokémon", Event = communityDay });
        task.Rewards.Add(new ResearchReward { FormId = "charizard", ShinyAvailable = true });
        db.ResearchTasks.Add(task);
        await db.SaveChangesAsync(Ct);
        return db;
    }

    private static IOptions<HttpJsonOptions> Json()
    {
        var options = new HttpJsonOptions();
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        return Options.Create(options);
    }

    private async Task<(int Status, HttpContext Http, byte[] Body)> GetAsync(AppDbContext db, int dex, Action<HttpRequest>? request = null)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Response.Body = new MemoryStream();
        request?.Invoke(http.Request);
        var result = await PokemonPageEndpoints.GetPokemonAsync(dex, db, _cache, Json(), http, Ct);
        await result.ExecuteAsync(http);
        return (http.Response.StatusCode, http, ((MemoryStream)http.Response.Body).ToArray());
    }

    private static PokemonPageResponse Read(byte[] body) => JsonSerializer.Deserialize<PokemonPageResponse>(body, _read)!;

    [Fact]
    public async Task ThePage_HasEveryVersion_WithItsNumbers()
    {
        await using var db = await SeedAsync();
        var (status, _, body) = await GetAsync(db, 6);

        Assert.Equal(StatusCodes.Status200OK, status);
        var page = Read(body);
        Assert.Equal(["charizard", "charizard_mega_y", "charizard_costume"], page.Forms.Select(f => f.Id));
        var charizard = page.Forms[0];
        Assert.Equal(("Charizard", null), (charizard.NameEn, charizard.LabelEn));
        Assert.Equal(["fire", "flying"], charizard.Types);
        Assert.Equal("pm6.fGIGANTAMAX", charizard.SpriteGigantamax);
        Assert.Null(page.Forms[1].SpriteGigantamax);
        Assert.Equal(("Mega Charizard Y", "Mega Y"), (page.Forms[1].NameEn, page.Forms[1].LabelEn));
        Assert.True(page.Forms[2].LooksOnly);
        Assert.False(charizard.LooksOnly);
        Assert.Equal([40m, 50m, 51m], charizard.MaxCp.Select(c => c.Level));
        Assert.Equal([15m, 20m, 25m, 30m, 35m], charizard.PerfectCp.Select(c => c.Level));
        Assert.True(charizard.MaxCp[0].Cp < charizard.MaxCp[1].Cp);
        Assert.Equal([("BLAST_BURN", MoveAvailability.EliteTm), ("FIRE_SPIN", MoveAvailability.Normal)], charizard.Movepool.Select(m => (m.MoveId, m.Availability)));
        Assert.Equal(2.56, charizard.Matchups.Single(m => m.Type == "rock").Multiplier, 2);

        var great = Assert.Single(charizard.Pvp);
        Assert.Equal([("DRAGON_CLAW", 900), ("FIRE_SPIN", 500)], great.Usage.Select(u => (u.MoveId, u.Uses)));
        Assert.Equal((League.Master, 15, 50m), (charizard.BestIvs[0].League, charizard.BestIvs[0].Attack, charizard.BestIvs[0].Level));
        Assert.Equal(League.Great, Assert.Single(page.Forms[1].BestIvs).League);
        Assert.Equal(("fire", 20), (charizard.RaidRanks[0].Type, charizard.RaidRanks[0].Rank));
        Assert.Equal([false, true], charizard.RaidMovesets.Select(m => m.IsShadow));
        Assert.Equal(1, Assert.Single(page.KeeperBoxes).AttackMax);

        // Only the moves the page names, not every move.
        Assert.Equal(["BLAST_BURN", "DRAGON_CLAW", "EMBER", "FIRE_SPIN"], page.Moves.Select(m => m.Id));
        Assert.Equal(101, page.CpMultipliers.Count);
    }

    [Fact]
    public async Task ThePage_HasTheFamily_TheEvolutions_AndTheCostumes()
    {
        await using var db = await SeedAsync();
        var page = Read((await GetAsync(db, 4)).Body);

        Assert.Equal(["charmander", "charmeleon", "charizard", "charizard_costume"], page.Family.Select(m => m.FormId));
        Assert.Equal("Charizard", page.Family[2].SpeciesNameEn);
        Assert.Equal([("charmander", "charmeleon", 25), ("charmeleon", "charizard", 100)], page.Evolutions.Select(e => (e.FromFormId, e.ToFormId, e.Candy)));
        Assert.Empty(page.Costumes);
        Assert.Equal("pm6.cJAN_2020_NOEVOLVE", Assert.Single(Read((await GetAsync(db, 6)).Body).Costumes).Sprite);
    }

    [Fact]
    public async Task Appearances_AreEveryKind_NewestFirst_WithoutRemovedEntries()
    {
        await using var db = await SeedAsync();
        var page = Read((await GetAsync(db, 6)).Body);

        Assert.Equal(
            [AppearanceKind.Raid, AppearanceKind.MaxBattle, AppearanceKind.Research, AppearanceKind.Event],
            page.Appearances.Select(a => a.Kind));
        var raid = page.Appearances[0];
        Assert.Equal(("charizard_mega_y", RaidTier.Mega, null), (raid.FormId, raid.RaidTier, raid.EventNameEn));
        Assert.Equal((6, true), (page.Appearances[1].MaxStars, page.Appearances[1].Gigantamax));
        Assert.Equal(("Catch 5 Fire-type Pokémon", "Community Day"), (page.Appearances[2].TaskEn, page.Appearances[2].EventNameEn));
        Assert.Equal(EventKind.CommunityDay, page.Appearances[3].EventKind);

        var egg = Assert.Single(Read((await GetAsync(db, 4)).Body).Appearances);
        Assert.Equal((AppearanceKind.Egg, EggKind.TwoKm, null), (egg.Kind, egg.Egg, egg.StartsAt));
    }

    [Fact]
    public async Task AnUnknownNumber_IsNotFound()
    {
        await using var db = await SeedAsync();
        var (status, _, _) = await GetAsync(db, 9999);
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal(0, _cache.Count);
    }

    [Fact]
    public async Task TheAnswer_IsCompressed_Cached_AndAnswers304ForItsETag()
    {
        await using var db = await SeedAsync();
        var (_, http, body) = await GetAsync(db, 6, request => request.Headers.AcceptEncoding = "gzip, br");
        Assert.Equal("br", http.Response.Headers.ContentEncoding.ToString());
        Assert.Equal("public, max-age=3600", http.Response.Headers.CacheControl.ToString());
        await using (var brotli = new BrotliStream(new MemoryStream(body), CompressionMode.Decompress))
        {
            Assert.Equal(6, JsonSerializer.Deserialize<PokemonPageResponse>(brotli, _read)!.Dex);
        }

        var (_, gzipped, _) = await GetAsync(db, 6, request => request.Headers.AcceptEncoding = "gzip");
        Assert.Equal("gzip", gzipped.Response.Headers.ContentEncoding.ToString());

        var etag = http.Response.Headers.ETag.ToString();
        var (status, _, empty) = await GetAsync(db, 6, request => request.Headers.IfNoneMatch = etag);
        Assert.Equal((StatusCodes.Status304NotModified, 0), (status, empty.Length));
        Assert.Equal(1, _cache.Count);

        // New live data: the answers are built again.
        db.JobRuns.Add(new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Succeeded, StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        var (again, _, _) = await GetAsync(db, 6, request => request.Headers.IfNoneMatch = etag);
        Assert.Equal(StatusCodes.Status304NotModified, again);
    }

    [Fact]
    public async Task TheCache_KeepsAtMostItsLimit_DroppingTheOldest()
    {
        var options = Json().Value.SerializerOptions;
        static PokemonPageResponse Page(int dex) => new(dex, "X", null, 1, SpeciesCategory.None, "F", [], [], [], [], [], [], [], []);
        for (var dex = 1; dex <= PokemonPageCache.MaxEntries + 5; dex++)
        {
            var current = dex;
            await _cache.GetAsync(dex, (null, null), () => Task.FromResult<PokemonPageResponse?>(Page(current)), options);
        }

        Assert.Equal(PokemonPageCache.MaxEntries, _cache.Count);
        var built = 0;
        await _cache.GetAsync(1, (null, null), () => { built++; return Task.FromResult<PokemonPageResponse?>(Page(1)); }, options);
        await _cache.GetAsync(PokemonPageCache.MaxEntries + 5, (null, null), () => { built++; return Task.FromResult<PokemonPageResponse?>(null); }, options);
        Assert.Equal(1, built);
    }
}
