namespace MyPoGoCompanion.Data.LiveData;

/// <summary>A Field Research task and its Pokémon rewards.</summary>
public class ResearchTask : FetchedEntry
{
    public required string TaskEn { get; set; }

    public string? TaskPt { get; set; }

    /// <summary>Set for event Field Research.</summary>
    public Guid? EventId { get; set; }

    public GameEvent? Event { get; set; }

    public List<ResearchReward> Rewards { get; } = [];
}
