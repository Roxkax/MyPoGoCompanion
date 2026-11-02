namespace MyPoGoCompanion.Engine.Types;

/// <summary>
/// Pokémon GO's type chart. Super effective ×1.6, not very effective ×0.625, and what the main games call "no effect"
/// ×0.390625 (GO has no immunities); dual types multiply (×2.56, ×0.390625 …). The screens round them to ×1.6,
/// ×0.63 and ×0.39.
/// </summary>
public static class TypeChart
{
    public const double SuperEffective = 1.6;
    public const double NotVeryEffective = 0.625;
    public const double DoubleResisted = 0.390625;

    /// <summary>The 18 types in the game's order.</summary>
    public static IReadOnlyList<string> Types { get; } =
    [
        "normal", "fire", "water", "electric", "grass", "ice", "fighting", "poison", "ground",
        "flying", "psychic", "bug", "rock", "ghost", "dragon", "dark", "steel", "fairy",
    ];

    // attacking type → (super effective against, not very effective against, "no effect" against)
    private static readonly Dictionary<string, (string[] Strong, string[] Weak, string[] None)> _chart = new(StringComparer.Ordinal)
    {
        ["normal"] = ([], ["rock", "steel"], ["ghost"]),
        ["fire"] = (["grass", "ice", "bug", "steel"], ["fire", "water", "rock", "dragon"], []),
        ["water"] = (["fire", "ground", "rock"], ["water", "grass", "dragon"], []),
        ["electric"] = (["water", "flying"], ["electric", "grass", "dragon"], ["ground"]),
        ["grass"] = (["water", "ground", "rock"], ["fire", "grass", "poison", "flying", "bug", "dragon", "steel"], []),
        ["ice"] = (["grass", "ground", "flying", "dragon"], ["fire", "water", "ice", "steel"], []),
        ["fighting"] = (["normal", "ice", "rock", "dark", "steel"], ["poison", "flying", "psychic", "bug", "fairy"], ["ghost"]),
        ["poison"] = (["grass", "fairy"], ["poison", "ground", "rock", "ghost"], ["steel"]),
        ["ground"] = (["fire", "electric", "poison", "rock", "steel"], ["grass", "bug"], ["flying"]),
        ["flying"] = (["grass", "fighting", "bug"], ["electric", "rock", "steel"], []),
        ["psychic"] = (["fighting", "poison"], ["psychic", "steel"], ["dark"]),
        ["bug"] = (["grass", "psychic", "dark"], ["fire", "fighting", "poison", "flying", "ghost", "steel", "fairy"], []),
        ["rock"] = (["fire", "ice", "flying", "bug"], ["fighting", "ground", "steel"], []),
        ["ghost"] = (["psychic", "ghost"], ["dark"], ["normal"]),
        ["dragon"] = (["dragon"], ["steel"], ["fairy"]),
        ["dark"] = (["psychic", "ghost"], ["fighting", "dark", "fairy"], []),
        ["steel"] = (["ice", "rock", "fairy"], ["fire", "water", "electric", "steel"], []),
        ["fairy"] = (["fighting", "dragon", "dark"], ["fire", "poison", "steel"], []),
    };

    /// <summary>The multiplier of one attacking type against one defending type.</summary>
    public static double Against(string attacking, string defending)
    {
        var row = Row(attacking);
        Row(defending);
        return row.Strong.Contains(defending, StringComparer.Ordinal) ? SuperEffective
            : row.Weak.Contains(defending, StringComparer.Ordinal) ? NotVeryEffective
            : row.None.Contains(defending, StringComparer.Ordinal) ? DoubleResisted
            : 1.0;
    }

    /// <summary>The multiplier of an attacking type against a Pokémon of one or two types (each type counted once).</summary>
    public static double Effectiveness(string attacking, IEnumerable<string> defending)
    {
        ArgumentNullException.ThrowIfNull(defending);
        var result = 1.0;
        foreach (var type in defending.Distinct(StringComparer.Ordinal))
        {
            result *= Against(attacking, type);
        }

        return result;
    }

    /// <summary>What every attacking type does to a Pokémon of these types, in the game's type order.</summary>
    public static IReadOnlyList<(string Type, double Multiplier)> Defending(IReadOnlyCollection<string> defending) =>
        [.. Types.Select(type => (type, Effectiveness(type, defending)))];

    private static (string[] Strong, string[] Weak, string[] None) Row(string type) =>
        _chart.TryGetValue(type ?? throw new ArgumentNullException(nameof(type)), out var row)
            ? row
            : throw new ArgumentException($"Unknown type \"{type}\".", nameof(type));
}
