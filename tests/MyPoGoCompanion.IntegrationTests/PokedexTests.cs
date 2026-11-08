using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Seed;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>The Pokédex list's data (E03-F01), read from a real database holding the development sample.</summary>
public sealed class PokedexTests(PostgresFixture postgres)
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

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string? encoding = null, string? etag = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/pokedex", UriKind.Relative));
        if (encoding is not null)
        {
            request.Headers.AcceptEncoding.ParseAdd(encoding);
        }

        if (etag is not null)
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Pokedex_HasEverySpeciesAndForm_FromTheDatabase()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        using var response = await GetAsync(client);
        response.EnsureSuccessStatusCode();
        var pokedex = await JsonSerializer.DeserializeAsync<PokedexResponse>(await response.Content.ReadAsStreamAsync(Ct), _json, Ct);

        await using var db = PostgresFixture.CreateContext(database);
        Assert.Equal(await db.Species.CountAsync(Ct), pokedex!.Species.Count);
        Assert.Equal(await db.Forms.CountAsync(Ct), pokedex.Species.Sum(s => s.Forms.Count));
        Assert.Equal(await db.Moves.CountAsync(Ct), pokedex.Moves.Count);
        Assert.Equal(await db.FormMoves.CountAsync(Ct), pokedex.Species.SelectMany(s => s.Forms).Sum(f => f.FastMoves.Count + f.ChargedMoves.Count));
        Assert.Equal(pokedex.Species.Select(s => s.Dex).Order(), pokedex.Species.Select(s => s.Dex));
        Assert.All(pokedex.Species, species => Assert.True(species.Forms[0].IsDefault, $"#{species.Dex}'s main form comes first"));
        Assert.All(pokedex.Species, species => Assert.Single(species.Forms, form => form.IsDefault));
    }

    [Fact]
    public async Task Pokedex_NamesTheVersions()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        using var response = await GetAsync(client);
        var pokedex = await JsonSerializer.DeserializeAsync<PokedexResponse>(await response.Content.ReadAsStreamAsync(Ct), _json, Ct);
        var forms = pokedex!.Species.SelectMany(s => s.Forms).ToDictionary(f => f.Id);

        await using var db = PostgresFixture.CreateContext(database);
        var mega = await db.Forms.AsNoTracking().Include(f => f.Species).FirstAsync(f => f.Kind == FormKind.Mega, Ct);
        Assert.StartsWith("Mega " + mega.Species.NameEn, forms[mega.Id].NameEn, StringComparison.Ordinal);
        Assert.StartsWith("Mega", forms[mega.Id].LabelEn, StringComparison.Ordinal);
        Assert.All(forms.Values.Where(f => f.IsDefault), form => Assert.Null(form.LabelEn));
    }

    [Fact]
    public async Task Pokedex_IsCompressed_AndUnchangedUntilAnImportIsPublished()
    {
        var database = await SeededDatabaseAsync();
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        using var first = await GetAsync(client, "br");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("br", Assert.Single(first.Content.Headers.ContentEncoding));
        Assert.Equal("public, max-age=3600", first.Headers.CacheControl?.ToString());
        await using (var brotli = new BrotliStream(await first.Content.ReadAsStreamAsync(Ct), CompressionMode.Decompress))
        {
            Assert.NotEmpty((await JsonSerializer.DeserializeAsync<PokedexResponse>(brotli, _json, Ct))!.Species);
        }

        var etag = first.Headers.ETag!.ToString();
        using (var again = await GetAsync(client, "br", etag))
        {
            Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
            Assert.Empty(await again.Content.ReadAsByteArrayAsync(Ct));
        }

        // A published import that changed a number brings a new answer (later than the sample's run, dated today 06:00).
        await using (var db = PostgresFixture.CreateContext(database))
        {
            (await db.Forms.FirstAsync(Ct)).Attack += 1;
            var finished = DateTimeOffset.UtcNow.AddDays(1);
            db.JobRuns.Add(new JobRun
            {
                Job = JobKind.DailyImport,
                Status = JobStatus.Succeeded,
                StartedAt = finished.AddMinutes(-1),
                FinishedAt = finished,
            });
            await db.SaveChangesAsync(Ct);
        }

        using var changed = await GetAsync(client, "gzip", etag);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal("gzip", Assert.Single(changed.Content.Headers.ContentEncoding));
        Assert.NotEqual(etag, changed.Headers.ETag!.ToString());
    }

    [Fact]
    public async Task Pokedex_EmptyDatabase_IsAnEmptyList()
    {
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct));
        using var client = api.CreateClient();

        using var response = await GetAsync(client);
        var pokedex = await JsonSerializer.DeserializeAsync<PokedexResponse>(await response.Content.ReadAsStreamAsync(Ct), _json, Ct);

        Assert.Empty(pokedex!.Species);
    }
}
