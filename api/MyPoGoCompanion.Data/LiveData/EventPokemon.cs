using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.LiveData;

/// <summary>A Pokémon an event features (spawns, the Community Day or Spotlight Pokémon, the debut).</summary>
public class EventPokemon
{
    public Guid EventId { get; set; }

    public GameEvent Event { get; set; } = null!;

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    /// <summary>Shiny available or boosted during the event.</summary>
    public bool Shiny { get; set; }

    public int SortOrder { get; set; }
}
