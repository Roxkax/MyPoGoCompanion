namespace MyPoGoCompanion.Engine;

/// <summary>
/// The game's CP multiplier per level, one value per half level from level 1 (PvPoke's <c>Pokemon.js</c>, the table
/// the Python keeper build used; the daily import stores it as <c>cp_multipliers</c>).
/// </summary>
public sealed class CpMultipliers
{
    private readonly double[] _values;

    /// <param name="values">Level 1, 1.5, 2 … in order; at least level 1.</param>
    public CpMultipliers(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = [.. values];
        if (_values.Length == 0)
        {
            throw new ArgumentException("At least level 1's multiplier is needed.", nameof(values));
        }

        if (_values.Any(value => !(value > 0)))
        {
            throw new ArgumentException("CP multipliers are positive numbers.", nameof(values));
        }
    }

    /// <summary>Number of levels in the table (level 1 … <see cref="MaxLevel"/> in half levels).</summary>
    public int Count => _values.Length;

    public decimal MaxLevel => LevelAt(_values.Length - 1);

    public double this[int index] => _values[index];

    /// <summary>The multiplier at a level (1, 1.5 … <see cref="MaxLevel"/>).</summary>
    public double At(decimal level) => _values[IndexOf(level)];

    /// <summary>Position of a level in the table: <c>(level − 1) × 2</c>.</summary>
    public int IndexOf(decimal level)
    {
        var index = (level - 1) * 2;
        if (index != decimal.Truncate(index) || index < 0 || index >= _values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Levels go from 1 to {MaxLevel} in half levels.");
        }

        return (int)index;
    }

    public static decimal LevelAt(int index) => 1 + (index / 2m);
}
