namespace MyPoGoCompanion.Engine.Raids;

/// <summary>A move's raid (PvE) numbers from the game master.</summary>
/// <param name="Id">PvPoke's move id (<c>FIRE_SPIN</c>).</param>
/// <param name="Type">Type id from the game master (<c>fire</c>).</param>
/// <param name="Duration">Seconds the move takes.</param>
/// <param name="DamageWindowStart">Seconds from the start until the hit lands (for dodging; charged moves).</param>
/// <param name="Energy">Energy gained (fast, positive) or spent (charged, negative).</param>
public sealed record RaidMove(string Id, string Type, double Power, double Duration, double DamageWindowStart, int Energy)
{
    public static RaidMove FromMilliseconds(string id, string type, double power, int durationMs, int damageWindowStartMs, int energy) =>
        new(id, type, power, durationMs / 1000.0, damageWindowStartMs / 1000.0, energy);
}

/// <summary>The raid numbers of every move, looked up by PvPoke id and kind.</summary>
public sealed class RaidMoveBook
{
    private readonly Dictionary<(string Id, bool Fast), RaidMove> _moves;

    public RaidMoveBook(IEnumerable<(string Id, bool Fast, RaidMove Move)> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        _moves = [];
        foreach (var (id, fast, move) in moves)
        {
            _moves[(id, fast)] = move;
        }
    }

    /// <summary>Moves a lookup asked for and didn't find (Hidden Power, Aegislash's charge moves…).</summary>
    public SortedSet<string> Missing { get; } = new(StringComparer.Ordinal);

    public RaidMove? Fast(string id) => Get(id, true);

    public RaidMove? Charged(string id) => Get(id, false);

    private RaidMove? Get(string id, bool fast)
    {
        if (_moves.TryGetValue((id, fast), out var move))
        {
            return move;
        }

        Missing.Add(fast ? id + " (fast)" : id);
        return null;
    }
}
