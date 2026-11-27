using MyPoGoCompanion.Data.LiveData;

namespace MyPoGoCompanion.Api.LiveData;

/// <summary>Resolves stored wall-clock times for a player, preserving the instant for global schedules.</summary>
internal sealed class LiveScheduleClock(TimeZoneInfo playerZone)
{
    public DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, playerZone).DateTime);

    public LiveSchedule? Resolve(IScheduled schedule)
    {
        var sourceZone = playerZone;
        if (schedule.TimeZone is { } zoneId && !TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out sourceZone))
        {
            return null;
        }

        var start = TimeZoneInfo.ConvertTime(AtWallTime(schedule.StartsAt, sourceZone), playerZone);
        DateTimeOffset? end = schedule.EndsAt is { } wallEnd ? TimeZoneInfo.ConvertTime(AtWallTime(wallEnd, sourceZone), playerZone) : null;
        return end <= start ? null : new LiveSchedule(start, end);
    }

    public bool Overlaps(LiveSchedule schedule, DateOnly first, DateOnly last)
    {
        var startDay = DateOnly.FromDateTime(schedule.StartsAt.DateTime);
        if (schedule.EndsAt is not { } end)
        {
            return startDay >= first && startDay <= last;
        }

        var lastInstant = TimeZoneInfo.ConvertTime(end.AddTicks(-1), playerZone);
        return startDay <= last && DateOnly.FromDateTime(lastInstant.DateTime) >= first;
    }

    public static bool IsCurrent(LiveSchedule schedule, DateTimeOffset now) =>
        schedule.StartsAt <= now && (schedule.EndsAt is null || schedule.EndsAt > now);

    private static DateTimeOffset AtWallTime(DateTime time, TimeZoneInfo zone)
    {
        var wall = DateTime.SpecifyKind(time, DateTimeKind.Unspecified);
        // A repeated time uses its first occurrence. A skipped time uses the pre-transition offset
        // (pass the equivalent later instant through ConvertTime to get a valid wall time).
        var offset = zone.IsAmbiguousTime(wall) ? zone.GetAmbiguousTimeOffsets(wall).Max()
            : zone.GetUtcOffset(zone.IsInvalidTime(wall) ? wall.AddDays(-1) : wall);
        return new DateTimeOffset(wall, offset);
    }
}
