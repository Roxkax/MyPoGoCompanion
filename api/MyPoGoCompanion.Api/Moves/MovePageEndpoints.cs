using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Api.Moves;

/// <summary>Move stats, learners ordered by availability and Attack, and dated event instructions.</summary>
public sealed record MovePageResponse(PageMove Move, IReadOnlyList<MoveLearner> Learners, IReadOnlyList<MoveEvent> Events);

/// <summary>A form that learns the move, with its base Attack and any Mega or Primal that shares its availability.</summary>
public sealed record MoveLearner(int Dex, string FormId, string NameEn, string? NamePt, IReadOnlyList<string> Types,
    int Attack, string? Sprite, MoveAvailability Availability, bool MegaToo, bool PrimalToo);

/// <summary>An event's wall-clock dates and move instructions. The event end may differ from the acquisition deadline.</summary>
public sealed record MoveEvent(Guid EventId, string FormId, string NameEn, string? NamePt, string PokemonNameEn, string? PokemonNamePt,
    string? HowEn, string? HowPt, DateTime StartsAt, DateTime? EndsAt, string? TimeZone);

internal static class MovePageEndpoints
{
    public static IServiceCollection AddMovePage(this IServiceCollection services)
    {
        services.AddSingleton<MovePageCache>();
        return services;
    }

    public static IEndpointRouteBuilder MapMovePageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/moves/{id}", GetMoveAsync)
            .WithName("GetMove")
            .WithTags("Moves")
            .WithSummary("A move with its stats, learners and dated event instructions.")
            .WithDescription(
                "Public and cacheable. Send the ETag back in If-None-Match to get 304 Not Modified until new game data or live data is published.")
            .Produces<MovePageResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    internal static async Task<IResult> GetMoveAsync(
        string id, AppDbContext db, MovePageCache cache, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, HttpContext http,
        CancellationToken cancellationToken)
    {
        id = id.Replace('-', '_').ToUpperInvariant();
        var stamp = await PokemonPageEndpoints.StampAsync(db, cancellationToken);
        var entry = await cache.GetAsync(id, stamp, () => BuildAsync(db, id, cancellationToken), json.Value.SerializerOptions);
        if (entry is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Move not found",
                detail: "There is no move with this id.");
        }

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

    internal static async Task<MovePageResponse?> BuildAsync(AppDbContext db, string id, CancellationToken cancellationToken)
    {
        var move = await db.Moves.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (move is null)
        {
            return null;
        }

        var learnset = await db.FormMoves.AsNoTracking().Include(row => row.Form).ThenInclude(form => form.Species)
            .Where(row => row.MoveId == id).ToListAsync(cancellationToken);
        var eventMoves = await db.EventMoves.AsNoTracking().Include(row => row.Event).Include(row => row.Form).ThenInclude(form => form.Species)
            .Where(row => row.MoveId == id && row.Event.RemovedAt == null)
            .OrderByDescending(row => row.Event.StartsAt).ThenBy(row => row.EventId).ThenBy(row => row.FormId)
            .ToListAsync(cancellationToken);

        return new MovePageResponse(new PageMove(move.Id, move.NameEn, move.NamePt, move.TypeId, move.Kind,
            move.RaidPower, move.RaidDurationMs, move.RaidEnergyDelta, move.PvpPower, move.PvpEnergyDelta,
            move.PvpTurns, move.PvpBuffChance, move.PvpSelfAttackStages, move.PvpSelfDefenseStages,
            move.PvpOpponentAttackStages, move.PvpOpponentDefenseStages),
            Learners(learnset),
            [.. eventMoves.Select(row => new MoveEvent(row.EventId, row.FormId, row.Event.NameEn, row.Event.NamePt,
                DisplayName(row.Form), row.Form.NamePt, row.HowEn, row.HowPt, row.Event.StartsAt, row.Event.EndsAt, row.Event.TimeZone))]);
    }

    private static string DisplayName(Form form) =>
        PokedexNames.DisplayNameEn(form.NameEn, form.Species.NameEn, form.Kind, form.IsDefault);

    internal static IReadOnlyList<MoveLearner> Learners(IReadOnlyList<FormMove> learnset)
    {
        var byForm = learnset.ToDictionary(row => row.FormId, StringComparer.Ordinal);
        bool SharesBaseRow(FormMove row) => row.Form.Kind is FormKind.Mega or FormKind.Primal
            && row.Form.BaseFormId is { } baseId && byForm.TryGetValue(baseId, out var baseMove)
            && baseMove.Form.Kind is not (FormKind.Mega or FormKind.Primal) && baseMove.Availability == row.Availability;
        var versions = learnset.Where(SharesBaseRow).ToLookup(row => row.Form.BaseFormId, StringComparer.Ordinal);
        return [.. learnset.Where(row => !SharesBaseRow(row))
            .OrderBy(row => row.Availability switch { MoveAvailability.EliteTm => 0, MoveAvailability.Legacy => 1, _ => 2 })
            .ThenByDescending(row => row.Form.Attack).ThenBy(row => row.Form.Dex).ThenBy(row => row.FormId, StringComparer.Ordinal)
            .Select(row => new MoveLearner(row.Form.Dex, row.FormId, DisplayName(row.Form), row.Form.NamePt,
                row.Form.Type2Id is null ? [row.Form.Type1Id] : [row.Form.Type1Id, row.Form.Type2Id],
                row.Form.Attack, row.Form.Sprite, row.Availability,
                versions[row.FormId].Any(version => version.Form.Kind == FormKind.Mega),
                versions[row.FormId].Any(version => version.Form.Kind == FormKind.Primal)))];
    }
}

internal sealed class MovePageCache : IDisposable
{
    internal const int MaxEntries = 300;

    private readonly SemaphoreSlim _building = new(1, 1);
    private readonly Dictionary<string, Entry> _entries = new();
    private (DateTimeOffset?, DateTimeOffset?) _stamp;

    public sealed record Entry(byte[] Json, byte[] Brotli, byte[] Gzip, string ETag, long Built);

    private long _counter;

    /// <returns>The answer; null when the move doesn't exist (not kept, so a new import can add it).</returns>
    public async Task<Entry?> GetAsync(
        string id, (DateTimeOffset?, DateTimeOffset?) stamp, Func<Task<MovePageResponse?>> build, JsonSerializerOptions options)
    {
        await _building.WaitAsync();
        try
        {
            if (_stamp != stamp)
            {
                _entries.Clear();
                _stamp = stamp;
            }

            if (_entries.TryGetValue(id, out var built))
            {
                return built;
            }

            var response = await build();
            if (response is null)
            {
                return null;
            }

            var json = JsonSerializer.SerializeToUtf8Bytes(response, options);
            var entry = new Entry(
                json,
                Compress(json, stream => new BrotliStream(stream, CompressionLevel.Optimal)),
                Compress(json, stream => new GZipStream(stream, CompressionLevel.Optimal)),
                $"\"{Convert.ToHexStringLower(SHA256.HashData(json))[..32]}\"",
                ++_counter);
            if (_entries.Count >= MaxEntries)
            {
                _entries.Remove(_entries.MinBy(pair => pair.Value.Built).Key);
            }

            _entries[id] = entry;
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
