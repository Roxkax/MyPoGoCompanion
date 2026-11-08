using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Rankings;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>How many rows of one kind the import added, changed and removed.</summary>
public sealed record KindChanges(int Added, int Changed, int Removed)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int Total => Added + Changed + Removed;
}

/// <summary>
/// Brings the database's game data in line with what the sources say, counting the differences per kind. Rows are
/// updated in place (the game's own ids are the keys). Types, families, species and moves are never removed, and a
/// form that disappears is marked not released, because live data and collections point at them (docs/DATABASE.md).
/// A form's movepool, its evolutions, the rankings and the engine's results follow the sources exactly. What other jobs fill in (shiny,
/// from the AI researcher; Portuguese text, from the game text) is kept when the sources don't say.
/// Call it inside a transaction and save afterwards: it saves once itself (see the main forms below), and the rest
/// is left for the caller's save.
/// </summary>
public static class GameDataWriter
{
    public static async Task<WriteResult> ApplyAsync(
        AppDbContext db, ImportedGameData data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(data);
        var formIds = data.Forms.Select(form => form.Id).ToHashSet(StringComparer.Ordinal);
        var names = await Names.LoadAsync(db, data, cancellationToken);
        var result = new WriteResult();

        // One main form per species is a unique index that PostgreSQL checks row by row, so a species whose main
        // form moves to another of its forms would clash half-way through the save: the old one is cleared and
        // saved first (inside the caller's transaction), and counted with the changed forms.
        var notMain = data.Forms.Where(form => !form.IsDefault).Select(form => form.Id).ToList();
        var lostMain = await db.Forms.Where(form => form.IsDefault && notMain.Contains(form.Id)).ToListAsync(cancellationToken);
        lostMain.ForEach(form => form.IsDefault = false);
        await db.SaveChangesAsync(cancellationToken);

        await result.SyncAsync("types", 0, db, db.Types, data.Types, type => type.Id, keep: null, remove: null, describe: null, cancellationToken);
        await result.SyncAsync("families", 0, db, db.Families, data.Families, family => family.Id, keep: null, remove: null, describe: null, cancellationToken);
        await result.SyncAsync("species", 0, db, db.Species, data.Species, species => species.Dex,
            keep: (current, incoming) => incoming.NamePt ??= current.NamePt, remove: null, describe: null, cancellationToken);
        await result.SyncAsync("forms", lostMain.Count, db, db.Forms, data.Forms, form => form.Id,
            keep: (current, incoming) =>
            {
                incoming.NamePt ??= current.NamePt;
                incoming.ShinyReleased ??= current.ShinyReleased;
            },
            // No longer listed: kept (live data and collections point at it), shown as not released.
            remove: form =>
            {
                if (!form.Released)
                {
                    return false;
                }

                form.Released = false;
                return true;
            },
            describe: form => new RemovedRow(form.Id, names.Form(form.Id)),
            cancellationToken);
        await result.SyncAsync("moves", 0, db, db.Moves, data.Moves, move => move.Id,
            keep: (current, incoming) => incoming.NamePt ??= current.NamePt, remove: null, describe: null, cancellationToken);
        await result.SyncAsync("movepools", 0, db, db.FormMoves, data.FormMoves, formMove => (formMove.FormId, formMove.MoveId),
            keep: null, remove: formMove => Delete(db, formMove, formIds.Contains(formMove.FormId)),
            describe: formMove => new RemovedRow($"{formMove.FormId}/{formMove.MoveId}", $"{names.Form(formMove.FormId)}: {names.Move(formMove.MoveId)}"),
            cancellationToken);
        await result.SyncAsync("evolutions", 0, db, db.Evolutions, data.Evolutions, evolution => (evolution.FromFormId, evolution.ToFormId),
            keep: (current, incoming) =>
            {
                incoming.Id = current.Id;
                incoming.TaskPt ??= current.TaskPt;
            },
            remove: evolution => Delete(db, evolution, formIds.Contains(evolution.FromFormId)),
            describe: evolution => new RemovedRow($"{evolution.FromFormId}/{evolution.ToFormId}", $"{names.Form(evolution.FromFormId)} → {names.Form(evolution.ToFormId)}"),
            cancellationToken);
        await result.SyncAsync("cpMultipliers", 0, db, db.CpMultipliers, data.CpMultipliers, multiplier => multiplier.Level,
            keep: null, remove: multiplier => Delete(db, multiplier, true),
            describe: multiplier => new RemovedRow(Level(multiplier.Level), $"Level {Level(multiplier.Level)}"),
            cancellationToken);
        await result.SyncAsync("costumes", 0, db, db.Costumes, data.Costumes, costume => costume.Sprite,
            keep: null, remove: costume => Delete(db, costume, true),
            describe: costume => new RemovedRow(costume.Sprite, $"#{Invariant(costume.Dex)} {costume.NameEn}"),
            cancellationToken);
        await result.SyncAsync("pvpRankings", 0, db, db.PvpRankings, data.PvpRankings, ranking => (ranking.League, ranking.FormId, ranking.IsShadow),
            keep: null, remove: ranking => Delete(db, ranking, true),
            describe: ranking => new RemovedRow($"{ranking.League}/{ranking.FormId}/{Shadow(ranking.IsShadow)}", $"{ranking.League} League: {names.Form(ranking.FormId, ranking.IsShadow)}"),
            cancellationToken);
        await result.SyncAsync("pvpBestIvs", 0, db, db.PvpBestIvs, data.PvpBestIvs, best => (best.League, best.FormId),
            keep: null, remove: best => Delete(db, best, true),
            describe: best => new RemovedRow($"{best.League}/{best.FormId}", $"{best.League} League: {names.Form(best.FormId)}"),
            cancellationToken);
        await result.SyncAsync("pvpKeeperBoxes", 0, db, db.PvpKeeperBoxes, data.PvpKeeperBoxes, box => (box.League, box.Dex),
            keep: null, remove: box => Delete(db, box, true),
            describe: box => new RemovedRow($"{box.League}/{Invariant(box.Dex)}", $"{box.League} League: #{Invariant(box.Dex)}"),
            cancellationToken);
        await result.SyncAsync("raidRankings", 0, db, db.RaidRankings, data.RaidRankings,
            ranking => (ranking.AttackingTypeId, ranking.FormId, ranking.IsShadow),
            keep: null, remove: ranking => Delete(db, ranking, true),
            describe: ranking => new RemovedRow($"{ranking.AttackingTypeId}/{ranking.FormId}/{Shadow(ranking.IsShadow)}", $"{names.Type(ranking.AttackingTypeId)}: {names.Form(ranking.FormId, ranking.IsShadow)}"),
            cancellationToken);
        await result.SyncAsync("raidMovesets", 0, db, db.RaidMovesets, data.RaidMovesets,
            moveset => (moveset.FormId, moveset.IsShadow, moveset.FastMoveId, moveset.ChargedMoveId),
            keep: null, remove: moveset => Delete(db, moveset, true),
            describe: moveset => new RemovedRow(
                $"{moveset.FormId}/{Shadow(moveset.IsShadow)}/{moveset.FastMoveId}/{moveset.ChargedMoveId}",
                $"{names.Form(moveset.FormId, moveset.IsShadow)}: {names.Move(moveset.FastMoveId)} + {names.Move(moveset.ChargedMoveId)}"),
            cancellationToken);
        await result.SyncAsync("pvpMoveUsage", 0, db, db.PvpMoveUsage, data.PvpMoveUsage,
            usage => (usage.League, usage.FormId, usage.IsShadow, usage.MoveId),
            keep: null, remove: usage => Delete(db, usage, true),
            describe: usage => new RemovedRow(
                $"{usage.League}/{usage.FormId}/{Shadow(usage.IsShadow)}/{usage.MoveId}",
                $"{usage.League} League: {names.Form(usage.FormId, usage.IsShadow)} uses {names.Move(usage.MoveId)}"),
            cancellationToken);
        return result;
    }

    private static bool Delete<T>(AppDbContext db, T row, bool when)
        where T : class
    {
        if (!when)
        {
            return false;
        }

        db.Remove(row);
        return true;
    }

    private static string Shadow(bool isShadow) => isShadow ? "shadow" : "normal";

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Level(decimal level) => level.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>English names for the removal list: the sources' names, else the stored ones, else the id.</summary>
    private sealed class Names
    {
        private readonly Dictionary<string, string> _forms;
        private readonly Dictionary<string, string> _moves;
        private readonly Dictionary<string, string> _types;

        private Names(Dictionary<string, string> forms, Dictionary<string, string> moves, Dictionary<string, string> types) =>
            (_forms, _moves, _types) = (forms, moves, types);

        public static async Task<Names> LoadAsync(AppDbContext db, ImportedGameData data, CancellationToken cancellationToken)
        {
            var forms = await db.Forms.AsNoTracking().ToDictionaryAsync(form => form.Id, form => form.NameEn, StringComparer.Ordinal, cancellationToken);
            var moves = await db.Moves.AsNoTracking().ToDictionaryAsync(move => move.Id, move => move.NameEn, StringComparer.Ordinal, cancellationToken);
            var types = await db.Types.AsNoTracking().ToDictionaryAsync(type => type.Id, type => type.NameEn, StringComparer.Ordinal, cancellationToken);
            data.Forms.ForEach(form => forms[form.Id] = form.NameEn);
            data.Moves.ForEach(move => moves[move.Id] = move.NameEn);
            data.Types.ForEach(type => types[type.Id] = type.NameEn);
            return new Names(forms, moves, types);
        }

        public string Form(string id, bool isShadow = false) => _forms.GetValueOrDefault(id, id) + (isShadow ? " (Shadow)" : "");

        public string Move(string id) => _moves.GetValueOrDefault(id, id);

        public string Type(string id) => _types.GetValueOrDefault(id, id);
    }
}

/// <summary>A row the sources no longer list: a stable key (for the fingerprint) and an English label.</summary>
public sealed record RemovedRow(string Key, string Label);

/// <summary>What <see cref="GameDataWriter.ApplyAsync"/> did: the counts per kind, and every row it would remove.</summary>
public sealed class WriteResult
{
    public Dictionary<string, KindChanges> Changes { get; } = new(StringComparer.Ordinal);

    /// <summary>Per kind, the rows removed (or, for forms, marked not released); kinds with none are left out.</summary>
    public Dictionary<string, List<RemovedRow>> Removals { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds what is new, copies the sources' values onto what exists (a row counts as changed only when a value
    /// differs) and offers every row the sources no longer list to <paramref name="remove"/>.
    /// <paramref name="alreadyChanged"/> rows were changed before (and so look unchanged here).
    /// </summary>
    internal async Task SyncAsync<T, TKey>(
        string kind,
        int alreadyChanged,
        AppDbContext db,
        DbSet<T> set,
        IEnumerable<T> incoming,
        Func<T, TKey> key,
        Action<T, T>? keep,
        Func<T, bool>? remove,
        Func<T, RemovedRow>? describe,
        CancellationToken cancellationToken)
        where T : class
        where TKey : notnull
    {
        var existing = await set.ToDictionaryAsync(key, cancellationToken);
        int added = 0, changed = alreadyChanged;
        foreach (var item in incoming)
        {
            if (existing.Remove(key(item), out var current))
            {
                keep?.Invoke(current, item);
                var entry = db.Entry(current);
                entry.CurrentValues.SetValues(item);
                if (entry.Properties.Any(property => property.IsModified))
                {
                    changed++;
                }
            }
            else
            {
                set.Add(item);
                added++;
            }
        }

        var removed = new List<RemovedRow>();
        if (remove is not null)
        {
            foreach (var row in existing.Values.Where(remove))
            {
                removed.Add(describe?.Invoke(row) ?? new RemovedRow(key(row).ToString() ?? "", key(row).ToString() ?? ""));
            }
        }

        Changes[kind] = new KindChanges(added, changed, removed.Count);
        if (removed.Count > 0)
        {
            Removals[kind] = removed;
        }
    }
}
