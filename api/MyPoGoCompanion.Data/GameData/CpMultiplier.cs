namespace MyPoGoCompanion.Data.GameData;

/// <summary>The game's CP multiplier for each level (1, 1.5 … 51).</summary>
public class CpMultiplier
{
    public decimal Level { get; set; }

    public double Value { get; set; }
}
