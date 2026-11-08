using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Engine.Types;

namespace MyPoGoCompanion.Api.Pokedex;

/// <summary>
/// The Pokémon page's data (E03-F02, decision 2026-11-08): one public answer per species with all its versions.
/// It changes only when a daily import or an AI researcher run is published, so each built answer is kept in memory
/// (already compressed) until one is, and has an ETag: browsers keep it an hour, then ask again and get
/// "304 Not Modified" until the data really changed.
/// </summary>
internal static class PokemonPageEndpoints
{
    public static IServiceCollection AddPokemonPage(this IServiceCollection services)
    {
        services.AddSingleton<PokemonPageCache>();
        return services;
    }

    public static IEndpointRouteBuilder MapPokemonPageEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/pokedex/{dex:int}", GetPokemonAsync)
            .WithName("GetPokemon")
            .WithTags("Pokédex")
            .WithSummary("One species with all its versions: everything the Pokémon page shows.")
            .WithDescription(
                "Public and cacheable. Send the ETag back in If-None-Match to get 304 Not Modified until new game data or live data is published.")
            .Produces<PokemonPageResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    internal static async Task<IResult> GetPokemonAsync(
        int dex, AppDbContext db, PokemonPageCache cache, IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, HttpContext http,
        CancellationToken cancellationToken)
    {
        var stamp = await StampAsync(db, cancellationToken);
        var entry = await cache.GetAsync(dex, stamp, () => BuildAsync(db, dex, cancellationToken), json.Value.SerializerOptions);
        if (entry is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Pokémon not found",
                detail: $"There is no Pokémon #{dex} in the Pokédex.");
        }

        var headers = http.Response.Headers;
        headers.CacheControl = $"public, max-age={PokedexEndpoints.CacheSeconds}";
        headers.ETag = entry.ETag;
        headers.Vary = HeaderNames.AcceptEncoding;

        if (http.Request.Headers.IfNoneMatch.Any(value => value is not null
                && value.Split(',').Any(tag => tag.Trim() == entry.ETag || tag.Trim() == "*")))
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

    /// <summary>When the data last changed: the last published daily import and AI researcher run.</summary>
    internal static async Task<(DateTimeOffset? GameData, DateTimeOffset? LiveData)> StampAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var runs = await db.JobRuns
            .Where(run => run.Status == JobStatus.Succeeded && (run.Job == JobKind.DailyImport || run.Job == JobKind.AiResearcher))
            .GroupBy(run => run.Job)
            .Select(group => new { Job = group.Key, Last = group.Max(run => run.FinishedAt) })
            .ToListAsync(cancellationToken);
        return (runs.FirstOrDefault(run => run.Job == JobKind.DailyImport)?.Last, runs.FirstOrDefault(run => run.Job == JobKind.AiResearcher)?.Last);
    }

    /// <summary>Reads one species and its family from the database; null when the Pokédex has no such number.</summary>
    internal static async Task<PokemonPageResponse?> BuildAsync(AppDbContext db, int dex, CancellationToken cancellationToken)
    {
        var species = await db.Species.AsNoTracking().SingleOrDefaultAsync(s => s.Dex == dex, cancellationToken);
        if (species is null)
        {
            return null;
        }

        var familySpecies = await db.Species.AsNoTracking().Where(s => s.FamilyId == species.FamilyId).OrderBy(s => s.Dex).ToListAsync(cancellationToken);
        var familyDexes = familySpecies.Select(s => s.Dex).ToList();
        var familyForms = await db.Forms.AsNoTracking().Where(form => familyDexes.Contains(form.Dex)).ToListAsync(cancellationToken);
        var own = familyForms.Where(form => form.Dex == dex).ToList();
        var ownIds = own.Select(form => form.Id).ToList();

        var movepool = await db.FormMoves.AsNoTracking().Where(fm => ownIds.Contains(fm.FormId)).ToListAsync(cancellationToken);
        var pvp = await db.PvpRankings.AsNoTracking().Where(r => ownIds.Contains(r.FormId)).ToListAsync(cancellationToken);
        var usage = await db.PvpMoveUsage.AsNoTracking().Where(u => ownIds.Contains(u.FormId)).ToListAsync(cancellationToken);
        var bestIvs = await db.PvpBestIvs.AsNoTracking().Where(b => ownIds.Contains(b.FormId)).ToListAsync(cancellationToken);
        var raidRanks = await db.RaidRankings.AsNoTracking().Where(r => ownIds.Contains(r.FormId)).ToListAsync(cancellationToken);
        var raidMovesets = await db.RaidMovesets.AsNoTracking().Where(r => ownIds.Contains(r.FormId)).ToListAsync(cancellationToken);
        var boxes = await db.PvpKeeperBoxes.AsNoTracking().Where(b => b.Dex == dex).OrderBy(b => b.League).ToListAsync(cancellationToken);
        var familyIds = familyForms.Select(form => form.Id).ToList();
        var evolutions = await db.Evolutions.AsNoTracking()
            .Where(e => familyIds.Contains(e.FromFormId) && familyIds.Contains(e.ToFormId))
            .OrderBy(e => e.Id).ToListAsync(cancellationToken);
        var costumes = await db.Costumes.AsNoTracking().Where(c => c.Dex == dex).OrderBy(c => c.Sprite).ToListAsync(cancellationToken);
        var multipliers = await db.CpMultipliers.AsNoTracking().OrderBy(m => m.Level).Select(m => m.Value).ToListAsync(cancellationToken);
        var appearances = await AppearancesAsync(db, ownIds, cancellationToken);

        var moveIds = movepool.Select(fm => fm.MoveId)
            .Concat(pvp.SelectMany(r => new[] { r.FastMoveId, r.ChargedMove1Id, r.ChargedMove2Id }).OfType<string>())
            .Concat(usage.Select(u => u.MoveId))
            .Concat(raidMovesets.SelectMany(r => new[] { r.FastMoveId, r.ChargedMoveId }))
            .Distinct(StringComparer.Ordinal).ToList();
        var moves = await db.Moves.AsNoTracking().Where(m => moveIds.Contains(m.Id)).OrderBy(m => m.Id).ToListAsync(cancellationToken);

        var cpm = multipliers.Count > 0 ? new CpMultipliers(multipliers) : null;
        var main = own.FirstOrDefault(form => form.IsDefault);
        var usageByRanking = usage.ToLookup(u => (u.League, u.FormId, u.IsShadow));

        PageForm ToForm(Form form)
        {
            var types = Types(form);
            var stats = new BaseStats(form.Attack, form.Defense, form.Stamina);
            IReadOnlyList<LevelCpRow> Cps(IEnumerable<decimal> levels) => cpm is null
                ? []
                : [.. levels.Where(level => level <= cpm.MaxLevel).Select(level => new LevelCpRow(level, Cp.Of(stats, Ivs.Perfect, level, cpm)))];
            return new PageForm(
                form.Id,
                form.Kind,
                form.IsDefault,
                PokedexNames.DisplayNameEn(form.NameEn, species.NameEn, form.Kind, form.IsDefault),
                form.NamePt,
                PokedexNames.LabelEn(form.NameEn, form.IsDefault),
                PokedexNames.LabelPt(form.NamePt, species.NamePt, form.IsDefault),
                types,
                form.Attack,
                form.Defense,
                form.Stamina,
                Cps(Cp.MaxCpLevels),
                Cps(Cp.CatchLevels),
                form.Released,
                form.ShadowReleased,
                form.ShinyReleased,
                form.CanDynamax,
                form.CanGigantamax,
                form.Sprite,
                form.CanGigantamax ? form.SpriteGigantamax : null,
                main is not null && !form.IsDefault && form.Kind is FormKind.Alternate or FormKind.Costume
                    && form.Attack == main.Attack && form.Defense == main.Defense && form.Stamina == main.Stamina
                    && form.Type1Id == main.Type1Id && form.Type2Id == main.Type2Id,
                form.BuddyDistanceKm,
                form.ThirdMoveStardust,
                [.. movepool.Where(fm => fm.FormId == form.Id).OrderBy(fm => fm.MoveId, StringComparer.Ordinal)
                    .Select(fm => new PageFormMove(fm.MoveId, fm.Availability))],
                [.. TypeChart.Defending(types).Select(m => new TypeMultiplier(m.Type, m.Multiplier))],
                [.. pvp.Where(r => r.FormId == form.Id).OrderBy(r => r.League).ThenBy(r => r.IsShadow)
                    .Select(r => new PagePvpRanking(r.League, r.IsShadow, r.Rank, r.Score, r.FastMoveId, r.ChargedMove1Id, r.ChargedMove2Id,
                        [.. usageByRanking[(r.League, r.FormId, r.IsShadow)].OrderByDescending(u => u.Uses).ThenBy(u => u.MoveId, StringComparer.Ordinal)
                            .Select(u => new MoveUses(u.MoveId, u.Uses))]))],
                [.. bestIvs.Where(b => b.FormId == form.Id).OrderBy(b => b.League)
                    .Select(b => new PageBestIvs(b.League, b.AttackIv, b.DefenseIv, b.StaminaIv, b.Level, b.Cp))],
                [.. raidRanks.Where(r => r.FormId == form.Id).OrderBy(r => r.Rank).ThenBy(r => r.AttackingTypeId, StringComparer.Ordinal)
                    .Select(r => new PageRaidRank(r.AttackingTypeId, r.IsShadow, r.Rank))],
                [.. raidMovesets.Where(r => r.FormId == form.Id).OrderBy(r => r.IsShadow).ThenBy(r => r.Rank)
                    .Select(r => new PageRaidMoveset(r.IsShadow, r.FastMoveId, r.ChargedMoveId, r.Rank, r.Dps, r.Tdo, r.Score))]);
        }

        var speciesNames = familySpecies.ToDictionary(s => s.Dex, s => s);
        return new PokemonPageResponse(
            species.Dex,
            species.NameEn,
            species.NamePt,
            species.Generation,
            species.Category,
            species.FamilyId,
            [.. own.OrderByDescending(f => f.IsDefault).ThenBy(f => PokedexEndpoints.KindOrder(f.Kind)).ThenBy(f => f.Id, StringComparer.Ordinal).Select(ToForm)],
            [.. boxes.Select(b => new PageKeeperBox(b.League, b.AttackBarMin, b.AttackBarMax, b.DefenseBarMin, b.DefenseBarMax, b.StaminaBarMin, b.StaminaBarMax))],
            [.. moves.Select(m => new PageMove(m.Id, m.NameEn, m.NamePt, m.TypeId, m.Kind, m.RaidPower, m.RaidDurationMs, m.RaidEnergyDelta,
                m.PvpPower, m.PvpEnergyDelta, m.PvpTurns, m.PvpBuffChance, m.PvpSelfAttackStages, m.PvpSelfDefenseStages,
                m.PvpOpponentAttackStages, m.PvpOpponentDefenseStages))],
            [.. familyForms.Where(f => f.Kind is not (FormKind.Mega or FormKind.Primal))
                .OrderBy(f => f.Dex).ThenByDescending(f => f.IsDefault).ThenBy(f => PokedexEndpoints.KindOrder(f.Kind)).ThenBy(f => f.Id, StringComparer.Ordinal)
                .Select(f => new FamilyMember(f.Dex, f.Id, f.Kind, f.IsDefault,
                    PokedexNames.DisplayNameEn(f.NameEn, speciesNames[f.Dex].NameEn, f.Kind, f.IsDefault), f.NamePt, speciesNames[f.Dex].NameEn,
                    Types(f), f.Attack, f.Defense, f.Stamina, f.Released, f.Sprite))],
            [.. evolutions.Select(e => new PageEvolution(e.FromFormId, e.ToFormId, e.Candy, e.CandyAfterTrade, e.ItemId, e.BuddyDistanceKm,
                e.TimeOfDay, e.LureId, e.Gender, e.TaskEn, e.TaskPt))],
            [.. costumes.Select(c => new PageCostume(c.Sprite, c.NameEn, c.FormCode))],
            appearances,
            multipliers);
    }

    private static string[] Types(Form form) => form.Type2Id is null ? [form.Type1Id] : [form.Type1Id, form.Type2Id];

    /// <summary>
    /// Every appearance of the versions in the live data, newest first; entries the admin removed are left out.
    /// Eggs and research have no dates of their own: they take their event's, else none.
    /// </summary>
    private static async Task<IReadOnlyList<PageAppearance>> AppearancesAsync(AppDbContext db, List<string> formIds, CancellationToken cancellationToken)
    {
        var raids = await db.RaidBosses.AsNoTracking().Include(r => r.Event)
            .Where(r => formIds.Contains(r.FormId) && r.RemovedAt == null).ToListAsync(cancellationToken);
        var maxes = await db.MaxBosses.AsNoTracking().Include(m => m.Event)
            .Where(m => formIds.Contains(m.FormId) && m.RemovedAt == null).ToListAsync(cancellationToken);
        var eggs = await db.EggPoolEntries.AsNoTracking().Include(e => e.Event)
            .Where(e => formIds.Contains(e.FormId) && e.RemovedAt == null).ToListAsync(cancellationToken);
        var rewards = await db.Set<ResearchReward>().AsNoTracking().Include(r => r.Task).ThenInclude(t => t.Event)
            .Where(r => formIds.Contains(r.FormId) && r.Task.RemovedAt == null).ToListAsync(cancellationToken);
        var featured = await db.EventPokemon.AsNoTracking().Include(p => p.Event)
            .Where(p => formIds.Contains(p.FormId) && p.Event.RemovedAt == null).ToListAsync(cancellationToken);

        static GameEvent? Shown(GameEvent? e) => e is { RemovedAt: null } ? e : null;

        var list = new List<PageAppearance>();
        list.AddRange(raids.Select(r => new PageAppearance(AppearanceKind.Raid, r.FormId, r.IsShadow, r.Tier, null, false, null, null, null,
            Shown(r.Event)?.NameEn, Shown(r.Event)?.NamePt, Shown(r.Event)?.Kind, r.StartsAt, r.EndsAt, r.ShinyAvailable)));
        list.AddRange(maxes.Select(m => new PageAppearance(AppearanceKind.MaxBattle, m.FormId, false, null, m.Stars, m.IsGigantamax, null, null, null,
            Shown(m.Event)?.NameEn, Shown(m.Event)?.NamePt, Shown(m.Event)?.Kind, m.StartsAt, m.EndsAt, m.ShinyAvailable)));
        list.AddRange(eggs.Select(e => new PageAppearance(AppearanceKind.Egg, e.FormId, false, null, null, false, e.Egg, null, null,
            Shown(e.Event)?.NameEn, Shown(e.Event)?.NamePt, Shown(e.Event)?.Kind, Shown(e.Event)?.StartsAt, Shown(e.Event)?.EndsAt, e.ShinyAvailable)));
        list.AddRange(rewards.Select(r => new PageAppearance(AppearanceKind.Research, r.FormId, false, null, null, false, null, r.Task.TaskEn, r.Task.TaskPt,
            Shown(r.Task.Event)?.NameEn, Shown(r.Task.Event)?.NamePt, Shown(r.Task.Event)?.Kind, Shown(r.Task.Event)?.StartsAt, Shown(r.Task.Event)?.EndsAt,
            r.ShinyAvailable)));
        list.AddRange(featured.Select(p => new PageAppearance(AppearanceKind.Event, p.FormId, false, null, null, false, null, null, null,
            p.Event.NameEn, p.Event.NamePt, p.Event.Kind, p.Event.StartsAt, p.Event.EndsAt, p.Shiny)));
        return [.. list
            .OrderByDescending(a => a.EndsAt ?? DateTime.MaxValue)
            .ThenByDescending(a => a.StartsAt ?? DateTime.MaxValue)
            .ThenBy(a => a.Kind)
            .ThenBy(a => a.FormId, StringComparer.Ordinal)
            .ThenBy(a => a.EventNameEn ?? a.TaskEn ?? "", StringComparer.Ordinal)];
    }
}

/// <summary>
/// The built answers per species for the last published data, as JSON and already compressed, with their ETag (a
/// hash of the JSON). Everything is dropped when new data is published. At most <see cref="MaxEntries"/> species are
/// kept; beyond that the oldest built go first.
/// </summary>
internal sealed class PokemonPageCache : IDisposable
{
    internal const int MaxEntries = 300;

    private readonly SemaphoreSlim _building = new(1, 1);
    private readonly ConcurrentDictionary<int, Entry> _entries = new();
    private (DateTimeOffset?, DateTimeOffset?) _stamp;

    public sealed record Entry(byte[] Json, byte[] Brotli, byte[] Gzip, string ETag, long Built);

    private long _counter;

    /// <returns>The answer; null when the species doesn't exist (not kept, so a new import can add it).</returns>
    public async Task<Entry?> GetAsync(
        int dex, (DateTimeOffset?, DateTimeOffset?) stamp, Func<Task<PokemonPageResponse?>> build, JsonSerializerOptions options)
    {
        if (_stamp == stamp && _entries.TryGetValue(dex, out var current))
        {
            return current;
        }

        await _building.WaitAsync();
        try
        {
            if (_stamp != stamp)
            {
                _entries.Clear();
                _stamp = stamp;
            }

            if (_entries.TryGetValue(dex, out var built))
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
                foreach (var oldest in _entries.OrderBy(pair => pair.Value.Built).Take(_entries.Count - MaxEntries + 1).ToList())
                {
                    _entries.TryRemove(oldest.Key, out _);
                }
            }

            _entries[dex] = entry;
            return entry;
        }
        finally
        {
            _building.Release();
        }
    }

    public int Count => _entries.Count;

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
