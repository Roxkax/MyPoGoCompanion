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
    public static async Task<IReadOnlyDictionary<string, KindChanges>> ApplyAsync(
        AppDbContext db, ImportedGameData data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(data);
        var formIds = data.Forms.Select(form => form.Id).ToHashSet(StringComparer.Ordinal);

        // One main form per species is a unique index that PostgreSQL checks row by row, so a species whose main
        // form moves to another of its forms would clash half-way through the save: the old one is cleared and
        // saved first (inside the caller's transaction), and counted with the changed forms.
        var notMain = data.Forms.Where(form => !form.IsDefault).Select(form => form.Id).ToList();
        var lostMain = await db.Forms.Where(form => form.IsDefault && notMain.Contains(form.Id)).ToListAsync(cancellationToken);
        lostMain.ForEach(form => form.IsDefault = false);
        await db.SaveChangesAsync(cancellationToken);
        var changes = new Dictionary<string, KindChanges>(StringComparer.Ordinal)
        {
            ["types"] = await SyncAsync(0, db, db.Types, data.Types, type => type.Id, keep: null, remove: null, cancellationToken),
            ["families"] = await SyncAsync(0, db, db.Families, data.Families, family => family.Id, keep: null, remove: null, cancellationToken),
            ["species"] = await SyncAsync(0, db, db.Species, data.Species, species => species.Dex,
                keep: (current, incoming) => incoming.NamePt ??= current.NamePt, remove: null, cancellationToken),
            ["forms"] = await SyncAsync(lostMain.Count, db, db.Forms, data.Forms, form => form.Id,
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
                cancellationToken),
            ["moves"] = await SyncAsync(0, db, db.Moves, data.Moves, move => move.Id,
                keep: (current, incoming) => incoming.NamePt ??= current.NamePt, remove: null, cancellationToken),
            ["movepools"] = await SyncAsync(0, db, db.FormMoves, data.FormMoves, formMove => (formMove.FormId, formMove.MoveId),
                keep: null, remove: formMove => Delete(db, formMove, formIds.Contains(formMove.FormId)), cancellationToken),
            ["evolutions"] = await SyncAsync(0, db, db.Evolutions, data.Evolutions, evolution => (evolution.FromFormId, evolution.ToFormId),
                keep: (current, incoming) =>
                {
                    incoming.Id = current.Id;
                    incoming.TaskPt ??= current.TaskPt;
                },
                remove: evolution => Delete(db, evolution, formIds.Contains(evolution.FromFormId)), cancellationToken),
            ["cpMultipliers"] = await SyncAsync(0, db, db.CpMultipliers, data.CpMultipliers, multiplier => multiplier.Level,
                keep: null, remove: multiplier => Delete(db, multiplier, true), cancellationToken),
            ["pvpRankings"] = await SyncAsync(0, db, db.PvpRankings, data.PvpRankings, ranking => (ranking.League, ranking.FormId, ranking.IsShadow),
                keep: null, remove: ranking => Delete(db, ranking, true), cancellationToken),
            ["pvpBestIvs"] = await SyncAsync(0, db, db.PvpBestIvs, data.PvpBestIvs, best => (best.League, best.FormId),
                keep: null, remove: best => Delete(db, best, true), cancellationToken),
            ["pvpKeeperBoxes"] = await SyncAsync(0, db, db.PvpKeeperBoxes, data.PvpKeeperBoxes, box => (box.League, box.Dex),
                keep: null, remove: box => Delete(db, box, true), cancellationToken),
            ["raidRankings"] = await SyncAsync(0, db, db.RaidRankings, data.RaidRankings,
                ranking => (ranking.AttackingTypeId, ranking.FormId, ranking.IsShadow),
                keep: null, remove: ranking => Delete(db, ranking, true), cancellationToken),
            ["raidMovesets"] = await SyncAsync(0, db, db.RaidMovesets, data.RaidMovesets,
                moveset => (moveset.FormId, moveset.IsShadow, moveset.FastMoveId, moveset.ChargedMoveId),
                keep: null, remove: moveset => Delete(db, moveset, true), cancellationToken),
        };
        return changes;
    }

    /// <summary>
    /// Adds what is new, copies the sources' values onto what exists (a row counts as changed only when a value
    /// differs) and offers every row the sources no longer list to <paramref name="remove"/>.
    /// <paramref name="alreadyChanged"/> rows were changed before (and so look unchanged here).
    /// </summary>
    private static async Task<KindChanges> SyncAsync<T, TKey>(
        int alreadyChanged,
        AppDbContext db,
        DbSet<T> set,
        IEnumerable<T> incoming,
        Func<T, TKey> key,
        Action<T, T>? keep,
        Func<T, bool>? remove,
        CancellationToken cancellationToken)
        where T : class
        where TKey : notnull
    {
        var existing = await set.ToDictionaryAsync(key, cancellationToken);
        int added = 0, changed = alreadyChanged, removed = 0;
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

        if (remove is not null)
        {
            removed = existing.Values.Count(remove);
        }

        return new KindChanges(added, changed, removed);
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
}
