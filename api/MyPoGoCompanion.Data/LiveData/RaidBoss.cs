using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.LiveData;

/// <summary>A boss in raids now or coming up.</summary>
public class RaidBoss : FetchedEntry, IScheduled
{
    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public bool IsShadow { get; set; }

    public RaidTier Tier { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string? TimeZone { get; set; }

    public bool ShinyAvailable { get; set; }

    /// <summary>The event it belongs to, for the event pill.</summary>
    public Guid? EventId { get; set; }

    public GameEvent? Event { get; set; }
}
