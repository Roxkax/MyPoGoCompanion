using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.LiveData;

/// <summary>A Pokémon that can hatch from an egg of one kind.</summary>
public class EggPoolEntry : FetchedEntry
{
    public EggKind Egg { get; set; }

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public bool ShinyAvailable { get; set; }

    /// <summary>Rarity tier 1–5 when the source gives one.</summary>
    public int? Rarity { get; set; }

    /// <summary>Set when the entry is only in the pool for an event.</summary>
    public Guid? EventId { get; set; }

    public GameEvent? Event { get; set; }
}
