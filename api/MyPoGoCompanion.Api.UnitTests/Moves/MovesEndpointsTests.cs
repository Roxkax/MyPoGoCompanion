using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Moves;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Operations;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace MyPoGoCompanion.Api.UnitTests.Moves;

public sealed class MovesEndpointsTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly MovesCache _cache = new();

    public void Dispose() => _cache.Dispose();

    private static async Task<AppDbContext> SeedAsync()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Moves.AddRange(
            new Move
            {
                Id = "MUD_SHOT",
                NameEn = "Mud Shot",
                NamePt = "Tiro de Lama",
                TypeId = "ground",
                Kind = MoveKind.Fast,
                RaidPower = 5,
                RaidDurationMs = 500,
                RaidEnergyDelta = 7,
                PvpPower = 4,
                PvpEnergyDelta = 8,
                PvpTurns = 2,
            },
            new Move
            {
                Id = "EARTH_POWER",
                NameEn = "Earth Power",
                TypeId = "ground",
                Kind = MoveKind.Charged,
                RaidPower = 100,
                RaidDurationMs = 3500,
                RaidEnergyDelta = -50,
                PvpPower = 90,
                PvpEnergyDelta = -55,
                PvpBuffChance = 0.1,
                PvpSelfAttackStages = 1,
                PvpSelfDefenseStages = 2,
                PvpOpponentAttackStages = -1,
                PvpOpponentDefenseStages = -2,
            });
        await db.SaveChangesAsync(Ct);
        return db;
    }

    private async Task<(int Status, HttpContext Http, byte[] Body)> GetAsync(AppDbContext db, string encoding = "", string? etag = null)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Headers.AcceptEncoding = encoding;
        http.Request.Headers.IfNoneMatch = etag;
        http.Response.Body = new MemoryStream();
        var options = new HttpJsonOptions();
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        var result = await MovesEndpoints.GetMovesAsync(db, _cache, Options.Create(options), http, Ct);
        await result.ExecuteAsync(http);
        return (http.Response.StatusCode, http, ((MemoryStream)http.Response.Body).ToArray());
    }

    [Fact]
    public async Task Build_ReturnsEveryMoveInStableOrder_WithBothNumberSetsAndAllEffects()
    {
        await using var db = await SeedAsync();
        var response = await MovesEndpoints.BuildAsync(db, Ct);

        Assert.Equal(
            new PageMove("EARTH_POWER", "Earth Power", null, "ground", MoveKind.Charged, 100, 3500, -50, 90, -55, null, 0.1, 1, 2, -1, -2),
            response.Moves[0]);
        Assert.Equal(
            new PageMove("MUD_SHOT", "Mud Shot", "Tiro de Lama", "ground", MoveKind.Fast, 5, 500, 7, 4, 8, 2, null, 0, 0, 0, 0),
            response.Moves[1]);
        Assert.Equal(2, response.Moves.Count);
    }

    [Theory]
    [InlineData("br, gzip", "br")]
    [InlineData("br;q=0, gzip", "gzip")]
    [InlineData("identity", "")]
    public async Task Get_SendsACompressedCacheableAnswer(string accept, string encoding)
    {
        await using var db = await SeedAsync();
        var (status, http, body) = await GetAsync(db, accept);

        Assert.Equal(200, status);
        Assert.Equal("public, max-age=3600", http.Response.Headers.CacheControl.ToString());
        Assert.Equal("Accept-Encoding", http.Response.Headers.Vary.ToString());
        Assert.Equal(encoding, http.Response.Headers.ContentEncoding.ToString());
        Assert.Matches("^\"[0-9a-f]{32}\"$", http.Response.Headers.ETag.ToString());
        await using Stream stream = encoding switch
        {
            "br" => new BrotliStream(new MemoryStream(body), CompressionMode.Decompress),
            "gzip" => new GZipStream(new MemoryStream(body), CompressionMode.Decompress),
            _ => new MemoryStream(body),
        };
        Assert.Equal(2, (await JsonSerializer.DeserializeAsync<MovesResponse>(stream, _json, Ct))!.Moves.Count);
    }

    [Fact]
    public async Task Get_MatchingTagsReturn304WithoutABody_AndUnrelatedTagsReturnData()
    {
        await using var db = await SeedAsync();
        var (_, first, _) = await GetAsync(db);
        var etag = first.Response.Headers.ETag.ToString();
        foreach (var condition in new[] { etag, $"W/{etag}", $"\"other\", {etag}", "*" })
        {
            var (status, http, body) = await GetAsync(db, "br", condition);
            Assert.Equal(304, status);
            Assert.Empty(body);
            Assert.Equal(etag, http.Response.Headers.ETag.ToString());
        }

        Assert.Equal(200, (await GetAsync(db, etag: "\"other\"")).Status);
    }

    [Fact]
    public async Task Get_OnlyPublishedImportsRefreshTheCache_AndUnchangedDataKeepsItsTag()
    {
        await using var db = await SeedAsync();
        var (_, before, _) = await GetAsync(db);
        var etag = before.Response.Headers.ETag.ToString();
        db.Moves.Single(move => move.Id == "MUD_SHOT").PvpPower = 99;
        await db.SaveChangesAsync(Ct);
        Assert.Equal(etag, (await GetAsync(db)).Http.Response.Headers.ETag.ToString());

        var now = DateTimeOffset.UtcNow;
        foreach (var status in new[] { JobStatus.Held, JobStatus.Failed, JobStatus.Succeeded })
        {
            db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = status, StartedAt = now, FinishedAt = now });
            await db.SaveChangesAsync(Ct);
            var changed = (await GetAsync(db)).Http.Response.Headers.ETag.ToString();
            Assert.Equal(status == JobStatus.Succeeded, changed != etag);
        }

        var publishedTag = (await GetAsync(db)).Http.Response.Headers.ETag.ToString();
        db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Succeeded, StartedAt = now, FinishedAt = now.AddMinutes(1) });
        await db.SaveChangesAsync(Ct);
        Assert.Equal(publishedTag, (await GetAsync(db)).Http.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task Cache_ConcurrentRequestsBuildOneAnswer()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builds = 0;
        async Task<MovesResponse> BuildAsync()
        {
            builds++;
            entered.SetResult();
            await release.Task;
            return new MovesResponse([]);
        }

        var first = _cache.GetAsync(null, BuildAsync, _json);
        await entered.Task;
        var second = _cache.GetAsync(null, BuildAsync, _json);
        release.SetResult();
        Assert.Same(await first, await second);
        Assert.Equal(1, builds);
    }

    [Fact]
    public void AddMoves_RegistersOneCache()
    {
        using var services = new ServiceCollection().AddMoves().BuildServiceProvider();
        Assert.Same(services.GetRequiredService<MovesCache>(), services.GetRequiredService<MovesCache>());
    }
}
