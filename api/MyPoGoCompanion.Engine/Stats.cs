namespace MyPoGoCompanion.Engine;

/// <summary>A form's base Attack, Defense and Stamina (HP), as the game master gives them.</summary>
public readonly record struct BaseStats(int Attack, int Defense, int Stamina);

/// <summary>Individual values, 0–15 each.</summary>
public readonly record struct Ivs(int Attack, int Defense, int Stamina)
{
    /// <summary>Number of IV spreads (16 × 16 × 16).</summary>
    public const int Count = 4096;

    public static Ivs Perfect { get; } = new(15, 15, 15);

    /// <summary>
    /// Every spread in the Python build's order (Attack, then Defense, then HP, each 0 → 15), so that index
    /// <c>a × 256 + d × 16 + h</c> is spread a/d/h and ties keep the same winner.
    /// </summary>
    public static IReadOnlyList<Ivs> All { get; } = Enumerable.Range(0, Count).Select(FromIndex).ToArray();

    public int Index => (Attack * 256) + (Defense * 16) + Stamina;

    public static Ivs FromIndex(int index) => new(index / 256, index / 16 % 16, index % 16);

    public bool IsValid => Attack is >= 0 and <= 15 && Defense is >= 0 and <= 15 && Stamina is >= 0 and <= 15;

    public override string ToString() => $"{Attack}/{Defense}/{Stamina}";
}

/// <summary>The appraisal's bars (0 = 0, 1 = 1–5, 2 = 6–10, 3 = 11–14, 4 = 15), which the game's search can filter by.</summary>
public static class Appraisal
{
    public const int Bars = 5;

    public static int Bar(int iv) => iv switch
    {
        < 0 or > 15 => throw new ArgumentOutOfRangeException(nameof(iv), iv, "IVs are 0–15."),
        0 => 0,
        <= 5 => 1,
        <= 10 => 2,
        <= 14 => 3,
        _ => 4,
    };

    /// <summary>The appraisal cell of a spread: <c>bar(a) × 25 + bar(d) × 5 + bar(h)</c> (125 cells).</summary>
    public static int Cell(Ivs ivs) => (Bar(ivs.Attack) * 25) + (Bar(ivs.Defense) * 5) + Bar(ivs.Stamina);
}
