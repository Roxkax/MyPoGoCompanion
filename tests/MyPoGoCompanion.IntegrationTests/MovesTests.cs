using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Moves;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Seed;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class MovesTests(PostgresFixture postgres)
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

    [Fact]
    public async Task Moves_PublicResponseMatchesTheDatabase_IncludingSignedEnergyAndEffects()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();
        using var response = await client.GetAsync("/moves", Ct);
        response.EnsureSuccessStatusCode();
        var list = (await JsonSerializer.DeserializeAsync<MovesResponse>(await response.Content.ReadAsStreamAsync(Ct), _json, Ct))!;

        await using var db = PostgresFixture.CreateContext(database);
        var stored = await db.Moves.AsNoTracking().OrderBy(move => move.Id).ToListAsync(Ct);
        Assert.NotEmpty(stored);
        Assert.Equal(stored.Select(move => move.Id), list.Moves.Select(move => move.Id));
        Assert.All(list.Moves, move =>
        {
            var original = stored.Single(row => row.Id == move.Id);
            Assert.Equal((original.NameEn, original.NamePt, original.TypeId, original.Kind), (move.NameEn, move.NamePt, move.Type, move.Kind));
            Assert.Equal((original.RaidPower, original.RaidDurationMs, original.RaidEnergyDelta), (move.RaidPower, move.RaidDurationMs, move.RaidEnergy));
            Assert.Equal((original.PvpPower, original.PvpEnergyDelta, original.PvpTurns, original.PvpBuffChance), (move.PvpPower, move.PvpEnergy, move.PvpTurns, move.PvpBuffChance));
            Assert.Equal((original.PvpSelfAttackStages, original.PvpSelfDefenseStages, original.PvpOpponentAttackStages, original.PvpOpponentDefenseStages),
                (move.PvpSelfAttack, move.PvpSelfDefense, move.PvpOpponentAttack, move.PvpOpponentDefense));
        });
    }

    [Fact]
    public async Task Moves_CompressedAnswerIsRevalidated_AndRefreshedAfterAPublishedImport()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
        using var first = await client.GetAsync("/moves", Ct);
        Assert.Equal("br", Assert.Single(first.Content.Headers.ContentEncoding));
        Assert.Equal("public, max-age=3600", first.Headers.CacheControl?.ToString());
        Assert.Contains("Accept-Encoding", first.Headers.Vary);
        await using (var stream = new BrotliStream(await first.Content.ReadAsStreamAsync(Ct), CompressionMode.Decompress))
        {
            Assert.NotEmpty((await JsonSerializer.DeserializeAsync<MovesResponse>(stream, _json, Ct))!.Moves);
        }

        client.DefaultRequestHeaders.IfNoneMatch.Add(EntityTagHeaderValue.Parse(first.Headers.ETag!.ToString()));
        using var unchanged = await client.GetAsync("/moves", Ct);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.Empty(await unchanged.Content.ReadAsByteArrayAsync(Ct));

        await using (var db = PostgresFixture.CreateContext(database))
        {
            (await db.Moves.FirstAsync(Ct)).PvpPower += 1;
            var finished = DateTimeOffset.UtcNow.AddDays(1);
            db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Succeeded, StartedAt = finished.AddMinutes(-1), FinishedAt = finished });
            await db.SaveChangesAsync(Ct);
        }

        using var changed = await client.GetAsync("/moves", Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(first.Headers.ETag, changed.Headers.ETag);
    }

    [Fact]
    public async Task Moves_EmptyDatabase_ReturnsAnEmptyList()
    {
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct));
        using var client = api.CreateClient();
        using var response = await client.GetAsync("/moves", Ct);
        response.EnsureSuccessStatusCode();
        Assert.Empty((await JsonSerializer.DeserializeAsync<MovesResponse>(await response.Content.ReadAsStreamAsync(Ct), _json, Ct))!.Moves);
    }
}
