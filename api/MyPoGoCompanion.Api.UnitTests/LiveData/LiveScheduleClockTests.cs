using MyPoGoCompanion.Api.LiveData;
using MyPoGoCompanion.Data.LiveData;

namespace MyPoGoCompanion.Api.UnitTests.LiveData;

public sealed class LiveScheduleClockTests
{
    [Fact]
    public void Resolve_PlayerLocalSchedule_KeepsTheWallTimeInEachZone()
    {
        var entry = Event(Wall(2026, 7, 11, 14), Wall(2026, 7, 11, 17));
        var lisbon = Clock("Europe/Lisbon").Resolve(entry)!;
        var tokyo = Clock("Asia/Tokyo").Resolve(entry)!;

        Assert.Equal(Wall(2026, 7, 11, 14), lisbon.StartsAt.DateTime);
        Assert.Equal(Wall(2026, 7, 11, 14), tokyo.StartsAt.DateTime);
        Assert.Equal(TimeSpan.FromHours(1), lisbon.StartsAt.Offset);
        Assert.Equal(TimeSpan.FromHours(9), tokyo.StartsAt.Offset);
    }

    [Fact]
    public void Resolve_GlobalSchedule_KeepsTheInstantAndCanMoveToAnotherDay()
    {
        var entry = Event(Wall(2026, 7, 11, 22), Wall(2026, 7, 12, 1), "America/New_York");
        var clock = Clock("Europe/Lisbon");
        var schedule = clock.Resolve(entry)!;

        Assert.Equal(new DateTimeOffset(2026, 7, 12, 3, 0, 0, TimeSpan.FromHours(1)), schedule.StartsAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 6, 0, 0, TimeSpan.FromHours(1)), schedule.EndsAt);
        Assert.Equal(new DateOnly(2026, 7, 12), clock.Today(new DateTimeOffset(2026, 7, 11, 23, 30, 0, TimeSpan.Zero)));
        Assert.Null(clock.Resolve(Event(entry.StartsAt, entry.EndsAt, "Invalid/TimeZone")));
    }

    [Fact]
    public void Resolve_DaylightSavingChanges_UseTheFirstOccurrenceAndShiftSkippedTimesForward()
    {
        var clock = Clock("America/New_York");
        var spring = clock.Resolve(Event(Wall(2026, 3, 8, 2), Wall(2026, 3, 8, 4)))!;
        var autumn = clock.Resolve(Event(Wall(2026, 11, 1, 1), Wall(2026, 11, 1, 2)))!;

        Assert.Equal(Wall(2026, 3, 8, 3), spring.StartsAt.DateTime);
        Assert.Equal(TimeSpan.FromHours(-4), spring.StartsAt.Offset);
        Assert.Equal(TimeSpan.FromHours(-4), autumn.StartsAt.Offset);
        Assert.Equal(TimeSpan.FromHours(-5), autumn.EndsAt!.Value.Offset);
        Assert.Null(clock.Resolve(Event(Wall(2026, 3, 8, 2).AddMinutes(30), Wall(2026, 3, 8, 3))));
    }

    [Fact]
    public void Calendar_OverlapsInProgressEvents_ExcludesAnEndAtMidnight_AndListsUnknownEndOnItsStartDay()
    {
        var clock = Clock("UTC");
        var first = new DateOnly(2026, 7, 11);
        var next = first.AddDays(1);
        var startedEarlier = clock.Resolve(Event(Wall(2026, 7, 10, 10), Wall(2026, 7, 12, 0)))!;
        var noEnd = clock.Resolve(Event(Wall(2026, 7, 12, 10), null))!;

        Assert.True(clock.Overlaps(startedEarlier, first, first));
        Assert.False(clock.Overlaps(startedEarlier, next, next));
        Assert.True(clock.Overlaps(noEnd, first, next));
        Assert.False(clock.Overlaps(noEnd, next.AddDays(1), next.AddDays(1)));
    }

    [Fact]
    public void IsCurrent_StartIsInclusive_EndIsExclusive()
    {
        var start = new DateTimeOffset(2026, 7, 11, 14, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(3);
        var schedule = new LiveSchedule(start, end);

        Assert.False(LiveScheduleClock.IsCurrent(schedule, start.AddTicks(-1)));
        Assert.True(LiveScheduleClock.IsCurrent(schedule, start));
        Assert.True(LiveScheduleClock.IsCurrent(schedule, end.AddTicks(-1)));
        Assert.False(LiveScheduleClock.IsCurrent(schedule, end));
        Assert.True(LiveScheduleClock.IsCurrent(new LiveSchedule(start, null), end));
    }

    private static LiveScheduleClock Clock(string zone) => new(TimeZoneInfo.FindSystemTimeZoneById(zone));

    private static DateTime Wall(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0);

    private static GameEvent Event(DateTime starts, DateTime? ends, string? zone = null) =>
        new() { Key = "test-event", NameEn = "Event", StartsAt = starts, EndsAt = ends, TimeZone = zone };
}
