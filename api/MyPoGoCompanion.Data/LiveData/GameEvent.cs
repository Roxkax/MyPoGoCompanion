namespace MyPoGoCompanion.Data.LiveData;

/// <summary>A calendar entry: Community Day, Spotlight Hour, Raid Hour, Max Monday, season, special event…</summary>
public class GameEvent : FetchedEntry, IScheduled
{
    public EventKind Kind { get; set; }

    public required string NameEn { get; set; }

    public string? NamePt { get; set; }

    /// <summary>One-line summary for the calendar and Today (e.g. "Ghost-types spawn more often").</summary>
    public string? SummaryEn { get; set; }

    public string? SummaryPt { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public string? TimeZone { get; set; }

    public List<EventPokemon> Pokemon { get; } = [];

    public List<EventMove> Moves { get; } = [];

    public List<EventBonus> Bonuses { get; } = [];
}
