using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.LiveData;

/// <summary>An exclusive move an event gives (e.g. Garchomp learns Earth Power when Gabite evolves).</summary>
public class EventMove
{
    public Guid EventId { get; set; }

    public GameEvent Event { get; set; } = null!;

    /// <summary>The form that ends up with the move.</summary>
    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public required string MoveId { get; set; }

    public Move Move { get; set; } = null!;

    /// <summary>How to get it (e.g. "Evolve Gabite 14:00–22:00").</summary>
    public string? HowEn { get; set; }

    public string? HowPt { get; set; }
}
