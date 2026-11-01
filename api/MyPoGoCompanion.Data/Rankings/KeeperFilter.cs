namespace MyPoGoCompanion.Data.Rankings;

public enum KeeperFilterKind
{
    GreatLeague,
    UltraLeague,
    Raid,
    MaxBattle,
    Mega,
}

/// <summary>
/// One part of a ready-made keeper filter (a search string of at most ~1,950 characters), rebuilt by the daily
/// import and checked by the game-search simulator before it is published.
/// </summary>
public class KeeperFilter
{
    public KeeperFilterKind Kind { get; set; }

    /// <summary>
    /// The option the card offers, empty for the default: <c>attack-15</c> (raid and Max: Attack 15 only) or
    /// <c>with-pre-evolutions</c> (Megas).
    /// </summary>
    public string Variant { get; set; } = string.Empty;

    /// <summary>1, 2, 3… when a filter is split into parts.</summary>
    public int Part { get; set; }

    /// <summary>First and last Pokédex numbers this part covers, when it is split by number.</summary>
    public int? FromDex { get; set; }

    public int? ToDex { get; set; }

    public required string SearchString { get; set; }
}
