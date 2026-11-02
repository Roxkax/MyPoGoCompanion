namespace MyPoGoCompanion.Engine;

/// <summary>The game's CP formula and the CP tables the screens show.</summary>
public static class Cp
{
    /// <summary>The lowest CP the game shows.</summary>
    public const int Minimum = 10;

    /// <summary>Levels a wild catch, raid, research or egg Pokémon is caught at (weather boost: +5).</summary>
    public static IReadOnlyList<decimal> CatchLevels { get; } = [15m, 20m, 25m, 30m, 35m];

    /// <summary>Levels the Pokémon page gives the 15/15/15 CP for (40, 50, 51 with a Best Buddy).</summary>
    public static IReadOnlyList<decimal> MaxCpLevels { get; } = [40m, 50m, 51m];

    /// <summary>
    /// <c>floor((Atk + a) × √((Def + d) × (HP + h)) × CPM² / 10)</c>, at least 10, in the Python build's order of
    /// operations so the rounding is the same.
    /// </summary>
    public static int Of(BaseStats stats, Ivs ivs, double cpm) =>
        Of(stats.Attack + ivs.Attack, stats.Defense + ivs.Defense, stats.Stamina + ivs.Stamina, cpm);

    public static int Of(BaseStats stats, Ivs ivs, decimal level, CpMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        return Of(stats, ivs, multipliers.At(level));
    }

    /// <summary>CP from the stats with IVs added (<paramref name="attack"/> = base Attack + Attack IV…).</summary>
    public static int Of(int attack, int defense, int stamina, double cpm) =>
        Math.Max(Minimum, (int)Math.Floor(attack * Math.Sqrt((double)defense * stamina) * (cpm * cpm) / 10));

    /// <summary>15/15/15 CP at each level of <paramref name="levels"/> (default: the catch levels).</summary>
    public static IReadOnlyList<LevelCp> Perfect(BaseStats stats, CpMultipliers multipliers, IEnumerable<decimal>? levels = null)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        return [.. (levels ?? CatchLevels).Select(level => new LevelCp(level, Of(stats, Ivs.Perfect, level, multipliers)))];
    }

    /// <summary>Max CP (15/15/15) at levels 40, 50 and 51.</summary>
    public static IReadOnlyList<LevelCp> Max(BaseStats stats, CpMultipliers multipliers) => Perfect(stats, multipliers, MaxCpLevels);
}

public readonly record struct LevelCp(decimal Level, int Cp);
