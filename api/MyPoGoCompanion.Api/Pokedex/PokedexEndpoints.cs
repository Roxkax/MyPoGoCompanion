using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Api.Pokedex;

/// <summary>
/// The Pokédex list's data (E03-F01, decision 2026-11-08): the whole Pokédex in one public answer that the app
/// searches and filters itself. The answer only changes when a daily import is published, so it is built once per
/// published import, kept in memory already compressed, and has an ETag: browsers keep it an hour, then ask again
/// and get "304 Not Modified" (no body) until the data really changed.
/// </summary>
internal static class PokedexEndpoints
{
    /// <summary>Browsers reuse the answer for an hour, then check it with its ETag.</summary>
    internal const int CacheSeconds = 3600;

    public static IServiceCollection AddPokedex(this IServiceCollection services)
    {
        services.AddSingleton<PokedexCache>();
        return services;
    }

    public static IEndpointRouteBuilder MapPokedexEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/pokedex", GetPokedexAsync)
            .WithName("GetPokedex")
            .WithTags("Pokédex")
            .WithSummary("The whole Pokédex: every species with its versions and moves, for the Pokédex list and its search.")
            .WithDescription(
                "Public and cacheable. Send the ETag back in If-None-Match to get 304 Not Modified until a new daily import is published.")
            .Produces<PokedexResponse>()
            .Produces(StatusCodes.Status304NotModified);

        return app;
    }

    internal static async Task<IResult> GetPokedexAsync(
        AppDbContext db, PokedexCache cache, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, HttpContext http,
        CancellationToken cancellationToken)
    {
        var published = await db.JobRuns
            .Where(run => run.Job == JobKind.DailyImport && run.Status == JobStatus.Succeeded)
            .MaxAsync(run => run.FinishedAt, cancellationToken);
        var entry = await cache.GetAsync(published, () => BuildAsync(db, cancellationToken), json.Value.SerializerOptions);

        var headers = http.Response.Headers;
        headers.CacheControl = $"public, max-age={CacheSeconds}";
        headers.ETag = entry.ETag;
        headers.Vary = HeaderNames.AcceptEncoding;

        if (http.Request.Headers.IfNoneMatch.Any(value => value is not null
                && value.Split(',').Any(tag => tag.Trim() == entry.ETag || tag.Trim() == "*")))
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var accepted = http.Request.Headers.AcceptEncoding.ToString();
        if (Accepts(accepted, "br"))
        {
            headers.ContentEncoding = "br";
            return Results.Bytes(entry.Brotli, "application/json");
        }

        if (Accepts(accepted, "gzip"))
        {
            headers.ContentEncoding = "gzip";
            return Results.Bytes(entry.Gzip, "application/json");
        }

        return Results.Bytes(entry.Json, "application/json");
    }

    internal static bool Accepts(string acceptEncoding, string coding) =>
        acceptEncoding.Split(',').Select(part => part.Split(';')).Any(part =>
            part[0].Trim().Equals(coding, StringComparison.OrdinalIgnoreCase)
            && !part.Skip(1).Any(parameter => parameter.Replace(" ", "", StringComparison.Ordinal) is "q=0" or "q=0.0" or "q=0.00" or "q=0.000"));

    /// <summary>Reads the Pokédex from the database: four queries, about 1,300 forms and 8,600 movepool entries.</summary>
    internal static async Task<PokedexResponse> BuildAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var species = await db.Species.AsNoTracking().OrderBy(s => s.Dex).ToListAsync(cancellationToken);
        var forms = await db.Forms.AsNoTracking().ToListAsync(cancellationToken);
        var learnable = await db.FormMoves.AsNoTracking()
            .Select(fm => new { fm.FormId, fm.MoveId, fm.Move.Kind })
            .ToListAsync(cancellationToken);
        var moves = await db.Moves.AsNoTracking().OrderBy(m => m.Id).ToListAsync(cancellationToken);

        var movesByForm = learnable
            .OrderBy(fm => fm.MoveId, StringComparer.Ordinal)
            .ToLookup(fm => fm.FormId, StringComparer.Ordinal);
        var formsByDex = forms.ToLookup(form => form.Dex);

        PokedexForm ToForm(Form form, Species of, Form? main)
        {
            var types = form.Type2Id is null ? new[] { form.Type1Id } : [form.Type1Id, form.Type2Id];
            var looksOnly = main is not null && !form.IsDefault && form.Kind is FormKind.Alternate or FormKind.Costume
                && form.Attack == main.Attack && form.Defense == main.Defense && form.Stamina == main.Stamina
                && form.Type1Id == main.Type1Id && form.Type2Id == main.Type2Id;
            var formMoves = movesByForm[form.Id];
            return new PokedexForm(
                form.Id,
                form.Kind,
                form.IsDefault,
                PokedexNames.DisplayNameEn(form.NameEn, of.NameEn, form.Kind, form.IsDefault),
                form.NamePt,
                PokedexNames.LabelEn(form.NameEn, form.IsDefault),
                PokedexNames.LabelPt(form.NamePt, of.NamePt, form.IsDefault),
                types,
                form.Attack,
                form.Defense,
                form.Stamina,
                form.MaxCp50,
                form.Released,
                form.ShadowReleased,
                form.CanDynamax,
                form.CanGigantamax,
                form.Sprite,
                form.CanGigantamax ? form.SpriteGigantamax : null,
                looksOnly,
                [.. formMoves.Where(fm => fm.Kind == MoveKind.Fast).Select(fm => fm.MoveId)],
                [.. formMoves.Where(fm => fm.Kind == MoveKind.Charged).Select(fm => fm.MoveId)]);
        }

        var list = species.Select(of =>
        {
            var own = formsByDex[of.Dex].ToList();
            var main = own.FirstOrDefault(form => form.IsDefault);
            var ordered = own
                .OrderByDescending(form => form.IsDefault)
                .ThenBy(form => KindOrder(form.Kind))
                .ThenBy(form => form.Id, StringComparer.Ordinal)
                .Select(form => ToForm(form, of, main))
                .ToList();
            return new PokedexSpecies(
                of.Dex, of.NameEn, of.NamePt, of.Generation, of.Category, of.FamilyId, of.Nicknames, ordered);
        }).ToList();

        return new PokedexResponse(
            list,
            [.. moves.Select(move => new PokedexMove(move.Id, move.NameEn, move.NamePt, move.TypeId, move.Kind))]);
    }

    /// <summary>The order of a species' versions: Megas and Primals, regional forms, other forms, looks.</summary>
    internal static int KindOrder(FormKind kind) => kind switch
    {
        FormKind.Base => 0,
        FormKind.Mega => 1,
        FormKind.Primal => 2,
        FormKind.Regional => 3,
        FormKind.Alternate => 4,
        _ => 5,
    };
}

/// <summary>
/// The built answer for the last published import, as JSON and already compressed, with its ETag (a hash of the
/// JSON, so an import that changed nothing keeps the same ETag and browsers keep their copy).
/// </summary>
internal sealed class PokedexCache : IDisposable
{
    private readonly SemaphoreSlim _building = new(1, 1);
    private Entry? _entry;

    public sealed record Entry(DateTimeOffset? Published, byte[] Json, byte[] Brotli, byte[] Gzip, string ETag);

    public async Task<Entry> GetAsync(DateTimeOffset? published, Func<Task<PokedexResponse>> build, JsonSerializerOptions options)
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
