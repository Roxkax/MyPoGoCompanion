using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Engine.Raids;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>The calculation engine's inputs, read from the same parsed files as the rest of the import.</summary>
public sealed record EngineInputs(IReadOnlyList<PokemonEntry> Entries, RaidMoveBook Moves, CpMultipliers Multipliers)
{
    /// <summary>
    /// Every PvPoke entry (Shadow versions included, in PvPoke's order) and the game master's raid numbers of every
    /// move they list, looked up as the Python build did (its aliases; no raid numbers for Hidden Power and
    /// Aegislash's charge moves).
    /// </summary>
    public static EngineInputs From(PvpokeGameMaster pvpoke, GameMasterSource gameMaster, IReadOnlyList<(decimal Level, double Value)> multipliers)
    {
        ArgumentNullException.ThrowIfNull(pvpoke);
        ArgumentNullException.ThrowIfNull(gameMaster);
        ArgumentNullException.ThrowIfNull(multipliers);
        var entries = pvpoke.Pokemon.Select(p => new PokemonEntry
        {
            Id = p.SpeciesId,
            Dex = p.Dex,
            Name = p.SpeciesName,
            Types = [.. p.Types.Where(type => type != "none")],
            Stats = new BaseStats(p.BaseStats.Atk, p.BaseStats.Def, p.BaseStats.Hp),
            IsMega = PokemonEntry.IsMegaId(p.SpeciesId, p.Tags),
            IsShadow = PokemonEntry.IsShadowId(p.SpeciesId, p.Tags),
            Released = p.Released,
            FastMoves = p.FastMoves,
            ChargedMoves = p.ChargedMoves,
            EliteMoves = p.EliteMoves,
            Parent = p.Family?.Parent,
            Evolutions = p.Family?.Evolutions ?? [],
        }).ToList();

        var book = new List<(string, bool, RaidMove)>();
        foreach (var (id, fast) in pvpoke.Pokemon.SelectMany(p => p.FastMoves.Select(id => (id, true)).Concat(p.ChargedMoves.Select(id => (id, false)))).Distinct())
        {
            if (GameDataBuilder.RaidNumbers(id, fast, gameMaster) is { } move)
            {
                book.Add((id, fast, RaidMove.FromMilliseconds(id, move.Type, move.Power, move.DurationMs, move.DamageWindowStartMs, move.EnergyDelta)));
            }
        }

        return new EngineInputs(entries, new RaidMoveBook(book), new CpMultipliers(multipliers.Select(m => m.Value)));
    }

    /// <summary>
    /// True when the table has every half level from 1 to 50 in order (PvPoke's always does; the engine reads it by
    /// position).
    /// </summary>
    public static bool HasEveryLevel(IReadOnlyList<(decimal Level, double Value)> multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        var needed = (int)((Engine.Pvp.Leagues.MaxLevel - 1) * 2) + 1;
        return multipliers.Count >= needed && multipliers.Select((m, i) => m.Level == CpMultipliers.LevelAt(i)).All(ok => ok);
    }
}
