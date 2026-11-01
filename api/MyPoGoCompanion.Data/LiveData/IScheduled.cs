namespace MyPoGoCompanion.Data.LiveData;

/// <summary>
/// How live-data times are stored. Most of the game's schedule runs on local time everywhere (raids rotate at
/// 10:00 local, Community Day is 14:00–17:00 local), so <c>starts_at</c>/<c>ends_at</c> are wall-clock times
/// without a zone, and <c>time_zone</c> is null. A global moment (e.g. a worldwide launch) has its IANA zone in
/// <c>time_zone</c> instead. The API turns both into the user's time.
/// </summary>
public interface IScheduled
{
    DateTime StartsAt { get; set; }

    DateTime? EndsAt { get; set; }

    /// <summary>Null: local time wherever the player is. Otherwise the IANA zone the times are given in.</summary>
    string? TimeZone { get; set; }
}
