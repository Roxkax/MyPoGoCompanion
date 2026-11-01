using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.LiveData;

/// <summary>A boss in Max Battles (power spots) now or coming up.</summary>
public class MaxBoss : FetchedEntry, IScheduled
{
    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    /// <summary>1–6 stars.</summary>
    public int Stars { get; set; }

    public bool IsGigantamax { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string? TimeZone { get; set; }

    public bool ShinyAvailable { get; set; }

    public Guid? EventId { get; set; }

    public GameEvent? Event { get; set; }
}
