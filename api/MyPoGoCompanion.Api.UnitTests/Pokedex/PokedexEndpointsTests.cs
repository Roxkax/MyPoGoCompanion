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
using MyPoGoCompanion.Data.Operations;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace MyPoGoCompanion.Api.UnitTests.Pokedex;

public sealed class PokedexEndpointsTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonSerializerOptions _read = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly PokedexCache _cache = new();

    public void Dispose() => _cache.Dispose();

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Form NewForm(string id, int dex, FormKind kind, string name, int attack, string type1 = "fire", string? type2 = "flying") => new()
    {
        Id = id,
        Dex = dex,
        Kind = kind,
        IsDefault = kind == FormKind.Base,
        NameEn = name,
        Type1Id = type1,
        Type2Id = type2,
        Attack = attack,
        Defense = 173,
        Stamina = 186,
        MaxCp50 = 3266,
        Released = true,
    };

    /// <summary>Charizard with a Mega, a made-up looks-only costume and a made-up form with its own stats; Pikachu with a costume.</summary>
    private static async Task<AppDbContext> SeedAsync()
    {
        var db = NewDb();
        db.Types.AddRange(
            new PokemonType { Id = "fire", NameEn = "Fire", NamePt = "Fogo" },
            new PokemonType { Id = "flying", NameEn = "Flying", NamePt = "Voador" },
            new PokemonType { Id = "electric", NameEn = "Electric", NamePt = "Elétrico" });
        db.Families.AddRange(new Family { Id = "FAMILY_CHARMANDER" }, new Family { Id = "FAMILY_PIKACHU" });
        db.Species.AddRange(
            new Species { Dex = 25, NameEn = "Pikachu", NamePt = "Pikachu", Generation = 1, FamilyId = "FAMILY_PIKACHU" },
            new Species { Dex = 6, NameEn = "Charizard", NamePt = "Charizard", Generation = 1, FamilyId = "FAMILY_CHARMANDER", Nicknames = ["zard"] });

        var charizard = NewForm("charizard", 6, FormKind.Base, "Charizard", 223);
        charizard.ShadowReleased = true;
        charizard.CanDynamax = true;
        charizard.CanGigantamax = true;
        charizard.Sprite = "pm6";
        charizard.SpriteGigantamax = "pm6.fGIGANTAMAX";
        var megaY = NewForm("charizard_mega_y", 6, FormKind.Mega, "Charizard (Mega Y)", 319);
        megaY.NamePt = "Mega Charizard Y";
        megaY.SpriteGigantamax = "left over";
        db.Forms.AddRange(
            megaY,
            NewForm("charizard_costume", 6, FormKind.Alternate, "Charizard (Costume)", 223),
            NewForm("charizard_strong", 6, FormKind.Alternate, "Charizard (Strong)", 250),
            NewForm("charizard_mega_x", 6, FormKind.Mega, "Charizard (Mega X)", 273, type2: "dragon"),
            charizard,
            NewForm("pikachu_libre", 25, FormKind.Alternate, "Pikachu (Libre)", 112, "electric", null),
            NewForm("pikachu", 25, FormKind.Base, "Pikachu", 112, "electric", null));
        db.Moves.AddRange(
            new Move { Id = "FIRE_SPIN", Kind = MoveKind.Fast, TypeId = "fire", NameEn = "Fire Spin", NamePt = "Chama Furacão" },
            new Move { Id = "EMBER", Kind = MoveKind.Fast, TypeId = "fire", NameEn = "Ember" },
            new Move { Id = "BLAST_BURN", Kind = MoveKind.Charged, TypeId = "fire", NameEn = "Blast Burn" });
        db.FormMoves.AddRange(
            new FormMove { FormId = "charizard", MoveId = "FIRE_SPIN" },
            new FormMove { FormId = "charizard", MoveId = "EMBER" },
            new FormMove { FormId = "charizard", MoveId = "BLAST_BURN", Availability = MoveAvailability.EliteTm });
        await db.SaveChangesAsync(Ct);
        return db;
    }

    private static IOptions<HttpJsonOptions> Json()
    {
        var options = new HttpJsonOptions();
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        return Options.Create(options);
    }

    private async Task<(int Status, HttpContext Http, byte[] Body)> GetAsync(AppDbContext db, Action<HttpRequest>? request = null)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Response.Body = new MemoryStream();
        request?.Invoke(http.Request);
        var result = await PokedexEndpoints.GetPokedexAsync(db, _cache, Json(), http, Ct);
        await result.ExecuteAsync(http);
        return (http.Response.StatusCode, http, ((MemoryStream)http.Response.Body).ToArray());
    }

    [Fact]
    public async Task Build_GivesEverySpeciesInOrder_WithItsVersionsMainFirst()
    {
        await using var db = await SeedAsync();

        var pokedex = await PokedexEndpoints.BuildAsync(db, Ct);

        Assert.Equal([6, 25], pokedex.Species.Select(s => s.Dex));
        var charizard = pokedex.Species[0];
        Assert.Equal(["zard"], charizard.Nicknames);
        Assert.Equal(
            ["charizard", "charizard_mega_x", "charizard_mega_y", "charizard_costume", "charizard_strong"],
            charizard.Forms.Select(f => f.Id));
        Assert.Equal(["EMBER", "FIRE_SPIN"], charizard.Forms[0].FastMoves);
        Assert.Equal(["BLAST_BURN"], charizard.Forms[0].ChargedMoves);
        Assert.Equal(["fire", "flying"], charizard.Forms[0].Types);
        Assert.Equal(["electric"], pokedex.Species[1].Forms[0].Types);
        Assert.Equal(3, pokedex.Moves.Count);
    }

    [Fact]
    public async Task Build_NamesVersions_AndMarksLooksOnlyForms()
    {
        await using var db = await SeedAsync();

        var forms = (await PokedexEndpoints.BuildAsync(db, Ct)).Species.SelectMany(s => s.Forms).ToDictionary(f => f.Id);

        var megaY = forms["charizard_mega_y"];
        Assert.Equal(("Mega Charizard Y", "Mega Y", "Mega Y"), (megaY.NameEn, megaY.LabelEn, megaY.LabelPt));
        Assert.Null(megaY.SpriteGigantamax); // only a form that can Gigantamax has that picture
        Assert.Equal("pm6.fGIGANTAMAX", forms["charizard"].SpriteGigantamax);
        Assert.Null(forms["charizard"].LabelEn);
        Assert.True(forms["charizard"].Shadow && forms["charizard"].Dynamax && forms["charizard"].Gigantamax);

        Assert.True(forms["charizard_costume"].LooksOnly);
        Assert.True(forms["pikachu_libre"].LooksOnly);
        Assert.False(forms["charizard_strong"].LooksOnly);
        Assert.False(forms["charizard_mega_y"].LooksOnly);
        Assert.False(forms["charizard"].LooksOnly);
    }

    [Fact]
    public async Task Get_SendsCompressedJson_WithACacheHourAndAnETag()
    {
        await using var db = await SeedAsync();

        var (status, http, body) = await GetAsync(db, request => request.Headers.AcceptEncoding = "gzip, deflate, br;q=0.9");

        Assert.Equal(200, status);
        Assert.Equal("br", http.Response.Headers.ContentEncoding.ToString());
        Assert.Equal("public, max-age=3600", http.Response.Headers.CacheControl.ToString());
        Assert.Equal("Accept-Encoding", http.Response.Headers.Vary.ToString());
        Assert.Matches("^\"[0-9a-f]{32}\"$", http.Response.Headers.ETag.ToString());
        await using var brotli = new BrotliStream(new MemoryStream(body), CompressionMode.Decompress);
        var pokedex = await JsonSerializer.DeserializeAsync<PokedexResponse>(brotli, _read, Ct);
        Assert.Equal(2, pokedex!.Species.Count);
    }

    [Theory]
    [InlineData("gzip", "gzip")]
    [InlineData("br;q=0, gzip", "gzip")]
    [InlineData("identity", "")]
    [InlineData("", "")]
    public async Task Get_PicksTheCompressionTheBrowserAccepts(string accept, string expected)
    {
        await using var db = await SeedAsync();

        var (_, http, body) = await GetAsync(db, request => request.Headers.AcceptEncoding = accept);

        Assert.Equal(expected, http.Response.Headers.ContentEncoding.ToString());
        Stream json = expected == "gzip" ? new GZipStream(new MemoryStream(body), CompressionMode.Decompress) : new MemoryStream(body);
        await using (json)
        {
            Assert.NotNull(await JsonSerializer.DeserializeAsync<PokedexResponse>(json, _read, Ct));
        }
    }

    [Fact]
    public async Task Get_WithTheSameETag_Answers304WithoutABody()
    {
        await using var db = await SeedAsync();
        var (_, first, _) = await GetAsync(db);
        var etag = first.Response.Headers.ETag.ToString();

        var (status, http, body) = await GetAsync(db, request => request.Headers.IfNoneMatch = $"\"other\", {etag}");

        Assert.Equal(304, status);
        Assert.Empty(body);
        Assert.Equal(etag, http.Response.Headers.ETag.ToString());
        Assert.Equal(200, (await GetAsync(db, request => request.Headers.IfNoneMatch = "\"other\"")).Status);
        Assert.Equal(304, (await GetAsync(db, request => request.Headers.IfNoneMatch = "*")).Status);
    }

    [Fact]
    public async Task Get_IsBuiltOncePerPublishedImport()
    {
        await using var db = await SeedAsync();
        var (_, before, _) = await GetAsync(db);

        // A change in the database alone isn't seen: the answer belongs to the last published import.
        db.Forms.Single(f => f.Id == "charizard").Attack = 999;
        await db.SaveChangesAsync(Ct);
        Assert.Equal(before.Response.Headers.ETag.ToString(), (await GetAsync(db)).Http.Response.Headers.ETag.ToString());

        // A held or failed run publishes nothing; a successful one does, and brings a new ETag.
        db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Held, StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        Assert.Equal(before.Response.Headers.ETag.ToString(), (await GetAsync(db)).Http.Response.Headers.ETag.ToString());

        db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Succeeded, StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        Assert.NotEqual(before.Response.Headers.ETag.ToString(), (await GetAsync(db)).Http.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task Get_ImportThatChangedNothing_KeepsTheETag()
    {
        await using var db = await SeedAsync();
        var (_, before, _) = await GetAsync(db);

        db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Succeeded, StartedAt = DateTimeOffset.UtcNow, FinishedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);

        Assert.Equal(before.Response.Headers.ETag.ToString(), (await GetAsync(db)).Http.Response.Headers.ETag.ToString());
    }

    [Theory]
    [InlineData(FormKind.Base, 0)]
    [InlineData(FormKind.Mega, 1)]
    [InlineData(FormKind.Primal, 2)]
    [InlineData(FormKind.Regional, 3)]
    [InlineData(FormKind.Alternate, 4)]
    [InlineData(FormKind.Costume, 5)]
    public void KindOrder_MegasFirstThenRegionalThenOthers(FormKind kind, int order) =>
        Assert.Equal(order, PokedexEndpoints.KindOrder(kind));

    [Fact]
    public void AddPokedex_RegistersOneCache()
    {
        using var services = new ServiceCollection().AddPokedex().BuildServiceProvider();
        Assert.Same(services.GetRequiredService<PokedexCache>(), services.GetRequiredService<PokedexCache>());
    }
}
