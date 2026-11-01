using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.LiveData;

/// <summary>A Pokémon encounter a research task can give.</summary>
public class ResearchReward
{
    public Guid TaskId { get; set; }

    public ResearchTask Task { get; set; } = null!;

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public bool ShinyAvailable { get; set; }
}
