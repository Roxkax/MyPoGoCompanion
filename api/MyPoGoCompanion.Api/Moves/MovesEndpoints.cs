using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Api.Moves;

/// <summary>The complete move list, searched and filtered in the app.</summary>
/// <param name="Moves">Every fast and charged move, including its raid numbers, PvP numbers and stat effects.</param>
public sealed record MovesResponse(IReadOnlyList<PageMove> Moves);

internal static class MovesEndpoints
{
    public static IServiceCollection AddMoves(this IServiceCollection services)
    {
        services.AddSingleton<MovesCache>();
        return services;
    }

    public static IEndpointRouteBuilder MapMovesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/moves", GetMovesAsync)
            .WithName("GetMoves")
            .WithTags("Moves")
            .WithSummary("Every fast and charged move, with raid and PvP numbers and PvP stat effects.")
            .WithDescription(
                "Public and cacheable. Send the ETag back in If-None-Match to get 304 Not Modified until new game data is published.")
            .Produces<MovesResponse>()
            .Produces(StatusCodes.Status304NotModified);
        return app;
    }

    internal static async Task<IResult> GetMovesAsync(
        AppDbContext db, MovesCache cache, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, HttpContext http,
        CancellationToken cancellationToken)
    {
        var published = await db.JobRuns
            .Where(run => run.Job == JobKind.DailyImport && run.Status == JobStatus.Succeeded)
            .MaxAsync(run => run.FinishedAt, cancellationToken);
        var entry = await cache.GetAsync(published, () => BuildAsync(db, cancellationToken), json.Value.SerializerOptions);

        var headers = http.Response.Headers;
        headers.CacheControl = $"public, max-age={PokedexEndpoints.CacheSeconds}";
        headers.ETag = entry.ETag;
        headers.Vary = HeaderNames.AcceptEncoding;

        if (http.Request.Headers.IfNoneMatch.Any(value => value is not null
                && value.Split(',').Any(tag => tag.Trim() == entry.ETag || tag.Trim() == $"W/{entry.ETag}" || tag.Trim() == "*")))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var accepted = http.Request.Headers.AcceptEncoding.ToString();
        if (PokedexEndpoints.Accepts(accepted, "br"))
        {
            headers.ContentEncoding = "br";
            return Results.Bytes(entry.Brotli, "application/json");
        }

        if (PokedexEndpoints.Accepts(accepted, "gzip"))
        {
            headers.ContentEncoding = "gzip";
            return Results.Bytes(entry.Gzip, "application/json");
        }

        return Results.Bytes(entry.Json, "application/json");
    }

    internal static async Task<MovesResponse> BuildAsync(AppDbContext db, CancellationToken cancellationToken) =>
        new(await db.Moves.AsNoTracking().OrderBy(move => move.Id)
            .Select(move => new PageMove(
                move.Id, move.NameEn, move.NamePt, move.TypeId, move.Kind,
                move.RaidPower, move.RaidDurationMs, move.RaidEnergyDelta,
                move.PvpPower, move.PvpEnergyDelta, move.PvpTurns, move.PvpBuffChance,
                move.PvpSelfAttackStages, move.PvpSelfDefenseStages, move.PvpOpponentAttackStages, move.PvpOpponentDefenseStages))
            .ToListAsync(cancellationToken));
}

/// <summary>One compressed answer per published import, matching the Pokédex list's cache policy.</summary>
internal sealed class MovesCache : IDisposable
{
    private readonly SemaphoreSlim _building = new(1, 1);
    private Entry? _entry;

    public sealed record Entry(DateTimeOffset? Published, byte[] Json, byte[] Brotli, byte[] Gzip, string ETag);

    public async Task<Entry> GetAsync(DateTimeOffset? published, Func<Task<MovesResponse>> build, JsonSerializerOptions options)
    {
        if (_entry is { } current && current.Published == published)
        {
            return current;
        }

        await _building.WaitAsync();
        try
        {
            if (_entry is { } built && built.Published == published)
            {
                return built;
            }

            var json = JsonSerializer.SerializeToUtf8Bytes(await build(), options);
            var entry = new Entry(
                published, json, Compress(json, stream => new BrotliStream(stream, CompressionLevel.SmallestSize)),
                Compress(json, stream => new GZipStream(stream, CompressionLevel.SmallestSize)),
                $"\"{Convert.ToHexStringLower(SHA256.HashData(json))[..32]}\"");
            _entry = entry;
            return entry;
        }
        finally
        {
            _building.Release();
        }
    }

    public void Dispose() => _building.Dispose();

    private static byte[] Compress(byte[] data, Func<Stream, Stream> compressor)
    {
        using var output = new MemoryStream();
        using (var stream = compressor(output))
        {
            stream.Write(data);
        }

        return output.ToArray();
    }
}
