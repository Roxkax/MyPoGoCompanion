namespace MyPoGoCompanion.Data.LiveData;

/// <summary>One bonus line of an event (e.g. "3× catch Stardust").</summary>
public class EventBonus
{
    public Guid EventId { get; set; }

    public GameEvent Event { get; set; } = null!;

    public int SortOrder { get; set; }

    public required string TextEn { get; set; }

    public string? TextPt { get; set; }
}
