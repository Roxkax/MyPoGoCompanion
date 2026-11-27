using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Engine;

namespace MyPoGoCompanion.Api.LiveData;

internal static class LiveDataEndpoints
{
    private const decimal ResearchLevel = 15m;
    private const decimal EggAndRaidLevel = 20m;
    private const decimal WeatherBoostedRaidLevel = 25m;
    private const int MaxCalendarDays = 31;
    private const string ZoneDescription = "Pass the player's timeZone (such as Europe/Lisbon). Defaults to UTC.";

    public static void MapLiveDataEndpoints(this IEndpointRouteBuilder app)
    {
        var live = app.MapGroup("").WithTags("Live data");
        live.MapGet("/raids", GetRaidsAsync)
            .WithName("GetRaids")
            .WithSummary("Current and upcoming raid bosses and their 100% catch CP.")
            .WithDescription(ZoneDescription)
            .Produces<RaidRotations>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        live.MapGet("/max-battles", GetMaxBattlesAsync)
            .WithName("GetMaxBattles")
            .WithSummary("Current and upcoming Dynamax and Gigantamax bosses.")
            .WithDescription(ZoneDescription)
            .Produces<MaxBattleRotations>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        live.MapGet("/events", GetEventsAsync)
            .WithName("GetCalendarEvents")
            .WithSummary("Events overlapping the requested player's calendar days.")
            .WithDescription($"Use date (YYYY-MM-DD) for the first day, and days (1–{MaxCalendarDays}) for the number of days. Defaults to today and one day. {ZoneDescription}")
            .Produces<LiveCalendarEvent[]>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        live.MapGet("/eggs", GetEggsAsync)
            .WithName("GetEggPools")
            .WithSummary("Current egg pool entries and their 100% hatch CP.")
            .WithDescription(ZoneDescription)
            .Produces<LiveEgg[]>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        live.MapGet("/research", GetResearchAsync)
            .WithName("GetFieldResearch")
            .WithSummary("Current Field Research tasks and their 100% encounter CP.")
            .WithDescription(ZoneDescription)
            .Produces<LiveResearchTask[]>()
            .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetRaidsAsync(string? timeZone, AppDbContext db, TimeProvider time, CancellationToken token)
    {
        if (!TryGetClock(timeZone, out var clock))
        {
            return InvalidZone();
        }

        var now = time.GetUtcNow();
        var earliestEnd = now.ToUniversalTime().DateTime.AddDays(-1);
        var runs = SuccessfulRuns(db);
        var bosses = await db.RaidBosses.AsNoTracking().Include(b => b.Event).Include(b => b.Form).ThenInclude(f => f.Species)
            .Where(b => b.RemovedAt == null &&
                (b.EndsAt > earliestEnd || b.EndsAt == null && (b.FixedAt != null || !runs.Any(start => start > b.FetchedAt))))
            .ToListAsync(token);
        var rotations = ActiveAndUpcoming(bosses, clock, now).ToList();

        var catchIds = rotations.Select(b =>
            {
                var id = CatchFormId(b.Entry.Form);
                return id != b.Entry.FormId ? id : null;
            })
            .OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        var catchForms = await db.Forms.AsNoTracking().Where(f => catchIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, token);
        var multipliers = await ReadMultipliersAsync(db, token, EggAndRaidLevel, WeatherBoostedRaidLevel);

        var current = new List<LiveRaidBoss>();
        var upcoming = new List<LiveRaidBoss>();
        foreach (var (boss, schedule) in rotations)
        {
            var catchId = CatchFormId(boss.Form);
            var caught = catchId == boss.FormId ? boss.Form : catchId is null ? null : catchForms.GetValueOrDefault(catchId);
            var row = new LiveRaidBoss(boss.Id, Pokemon(boss.Form), boss.Tier, boss.IsShadow, boss.ShinyAvailable,
                schedule, EventReference(boss.Event), catchId,
                PerfectCp(caught, EggAndRaidLevel, multipliers), PerfectCp(caught, WeatherBoostedRaidLevel, multipliers));
            (schedule.StartsAt <= now ? current : upcoming).Add(row);
        }

        return Results.Ok(new RaidRotations(current, upcoming));
    }

    private static async Task<IResult> GetMaxBattlesAsync(string? timeZone, AppDbContext db, TimeProvider time, CancellationToken token)
    {
        if (!TryGetClock(timeZone, out var clock))
        {
            return InvalidZone();
        }

        var now = time.GetUtcNow();
        var earliestEnd = now.ToUniversalTime().DateTime.AddDays(-1);
        var runs = SuccessfulRuns(db);
        var bosses = await db.MaxBosses.AsNoTracking().Include(b => b.Event).Include(b => b.Form).ThenInclude(f => f.Species)
            .Where(b => b.RemovedAt == null &&
                (b.EndsAt > earliestEnd || b.EndsAt == null && (b.FixedAt != null || !runs.Any(start => start > b.FetchedAt))))
            .ToListAsync(token);

        var current = new List<LiveMaxBoss>();
        var upcoming = new List<LiveMaxBoss>();
        foreach (var (boss, schedule) in ActiveAndUpcoming(bosses, clock, now))
        {
            var row = new LiveMaxBoss(boss.Id, Pokemon(boss.Form), boss.Stars, boss.IsGigantamax, boss.ShinyAvailable, schedule, EventReference(boss.Event));
            (schedule.StartsAt <= now ? current : upcoming).Add(row);
        }

        return Results.Ok(new MaxBattleRotations(current, upcoming));
    }

    private static async Task<IResult> GetEventsAsync(
        DateOnly? date, int? days, string? timeZone, AppDbContext db, TimeProvider time, CancellationToken token)
    {
        if (!TryGetClock(timeZone, out var clock))
        {
            return InvalidZone();
        }

        var first = date ?? clock.Today(time.GetUtcNow());
        var count = days ?? 1;
        if (count is < 1 or > MaxCalendarDays || first.DayNumber > DateOnly.MaxValue.DayNumber - (count - 1))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid calendar range",
                detail: $"Choose between 1 and {MaxCalendarDays} days within the supported calendar.");
        }

        var last = first.AddDays(count - 1);
        // The stored wall clock and the player's wall clock can differ by more than a day across the date line.
        var earliest = DateOnly.FromDayNumber(Math.Max(0, first.DayNumber - 2)).ToDateTime(TimeOnly.MinValue);
        var latest = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, last.DayNumber + 2)).ToDateTime(TimeOnly.MaxValue);
        var events = await db.Events.AsNoTracking().AsSingleQuery()
            .Where(e => e.RemovedAt == null && e.StartsAt <= latest &&
                (e.EndsAt > earliest || e.EndsAt == null && e.StartsAt >= earliest))
            .Include(e => e.Pokemon).ThenInclude(p => p.Form).ThenInclude(f => f.Species)
            .Include(e => e.Moves).ThenInclude(m => m.Form).ThenInclude(f => f.Species)
            .Include(e => e.Moves).ThenInclude(m => m.Move)
            .Include(e => e.Bonuses)
            .ToListAsync(token);

        var result = (from entry in events
                      let schedule = clock.Resolve(entry)
                      where schedule != null && clock.Overlaps(schedule, first, last)
                      orderby schedule.StartsAt, entry.Id
                      select new LiveCalendarEvent(
                          entry.Id, entry.Kind, entry.NameEn, entry.NamePt, entry.SummaryEn, entry.SummaryPt, schedule,
                          [.. entry.Pokemon.OrderBy(p => p.SortOrder).ThenBy(p => p.FormId, StringComparer.Ordinal)
                              .Select(p => new LiveEventPokemon(Pokemon(p.Form), p.Shiny))],
                          [.. entry.Moves.OrderBy(m => m.MoveId, StringComparer.Ordinal).ThenBy(m => m.FormId, StringComparer.Ordinal)
                              .Select(m => new LiveEventMove(Pokemon(m.Form), m.MoveId, m.Move.NameEn, m.Move.NamePt, m.HowEn, m.HowPt))],
                          [.. entry.Bonuses.OrderBy(b => b.SortOrder).Select(b => new LiveEventBonus(b.TextEn, b.TextPt))]))
            .ToArray();
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEggsAsync(string? timeZone, AppDbContext db, TimeProvider time, CancellationToken token)
    {
        if (!TryGetClock(timeZone, out var clock))
        {
            return InvalidZone();
        }

        var now = time.GetUtcNow();
        var runs = SuccessfulRuns(db);
        var eggs = await db.EggPoolEntries.AsNoTracking().Include(e => e.Event).Include(e => e.Form).ThenInclude(f => f.Species)
            .Where(e => e.RemovedAt == null &&
                (e.Event == null || e.Event.RemovedAt == null) &&
                (e.Event != null && e.Event.EndsAt != null || e.FixedAt != null || !runs.Any(start => start > e.FetchedAt)))
            .OrderBy(e => e.Egg).ThenBy(e => e.Rarity == null).ThenBy(e => e.Rarity).ThenBy(e => e.Form.Dex).ThenBy(e => e.FormId).ThenBy(e => e.Id)
            .ToListAsync(token);
        var multipliers = await ReadMultipliersAsync(db, token, EggAndRaidLevel);
        return Results.Ok(eggs.Where(e => EventIsCurrent(e.Event, clock, now))
            .Select(e => new LiveEgg(e.Egg, Pokemon(e.Form), e.ShinyAvailable, e.Rarity, EventReference(e.Event), PerfectCp(e.Form, EggAndRaidLevel, multipliers)))
            .ToArray());
    }

    private static async Task<IResult> GetResearchAsync(string? timeZone, AppDbContext db, TimeProvider time, CancellationToken token)
    {
        if (!TryGetClock(timeZone, out var clock))
        {
            return InvalidZone();
        }

        var now = time.GetUtcNow();
        var runs = SuccessfulRuns(db);
        var tasks = await db.ResearchTasks.AsNoTracking().Include(t => t.Event).Include(t => t.Rewards).ThenInclude(r => r.Form).ThenInclude(f => f.Species)
            .Where(t => t.RemovedAt == null &&
                (t.Event == null || t.Event.RemovedAt == null) &&
                (t.Event != null && t.Event.EndsAt != null || t.FixedAt != null || !runs.Any(start => start > t.FetchedAt)))
            .OrderBy(t => t.TaskEn).ThenBy(t => t.Id)
            .ToListAsync(token);
        var multipliers = await ReadMultipliersAsync(db, token, ResearchLevel);
        return Results.Ok(tasks.Where(t => EventIsCurrent(t.Event, clock, now))
            .Select(t => new LiveResearchTask(t.Id, t.TaskEn, t.TaskPt, EventReference(t.Event),
                [.. t.Rewards.OrderBy(r => r.Form.Dex).ThenBy(r => r.FormId, StringComparer.Ordinal)
                    .Select(r => new LiveResearchReward(Pokemon(r.Form), r.ShinyAvailable, PerfectCp(r.Form, ResearchLevel, multipliers)))]))
            .ToArray());
    }

    // Each completed run publishes atomically with the observations made during it. Failed runs leave the last
    // successful snapshot in use. Dates and admin fixes can keep an entry visible independently of the snapshot.
    private static IQueryable<DateTimeOffset> SuccessfulRuns(AppDbContext db) =>
        db.JobRuns.Where(r => r.Job == JobKind.AiResearcher && r.Status == JobStatus.Succeeded).Select(r => r.StartedAt);

    private static bool TryGetClock(string? timeZone, [NotNullWhen(true)] out LiveScheduleClock? clock)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById(timeZone ?? "UTC", out var zone))
        {
            clock = new LiveScheduleClock(zone);
            return true;
        }

        clock = null;
        return false;
    }

    private static IResult InvalidZone() => Results.Problem(statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid time zone", detail: "Use a valid time zone identifier such as Europe/Lisbon.");

    private static IEnumerable<(T Entry, LiveSchedule Schedule)> ActiveAndUpcoming<T>(
        IEnumerable<T> entries, LiveScheduleClock clock, DateTimeOffset now) where T : FetchedEntry, IScheduled =>
        from entry in entries
        let schedule = clock.Resolve(entry)
        where schedule != null && (schedule.EndsAt is null || schedule.EndsAt > now)
        orderby schedule.StartsAt, entry.Id
        select (entry, schedule);

    private static bool EventIsCurrent(GameEvent? entry, LiveScheduleClock clock, DateTimeOffset now) =>
        entry is null || clock.Resolve(entry) is { } schedule && LiveScheduleClock.IsCurrent(schedule, now);

    private static LiveEventReference? EventReference(GameEvent? entry) =>
        entry is { RemovedAt: null } ? new LiveEventReference(entry.Id, entry.NameEn, entry.NamePt) : null;

    private static LivePokemon Pokemon(Form form) => new(form.Dex, form.Id,
        PokedexNames.DisplayNameEn(form.NameEn, form.Species.NameEn, form.Kind, form.IsDefault),
        form.NamePt, form.Type2Id is null ? [form.Type1Id] : [form.Type1Id, form.Type2Id], form.Sprite, form.SpriteShiny, form.SpriteGigantamax);

    private static Task<Dictionary<decimal, double>> ReadMultipliersAsync(AppDbContext db, CancellationToken token, params decimal[] levels) =>
        db.CpMultipliers.AsNoTracking().Where(m => levels.Contains(m.Level)).ToDictionaryAsync(m => m.Level, m => m.Value, token);

    internal static int? PerfectCp(Form? form, decimal level, IReadOnlyDictionary<decimal, double> multipliers) =>
        form is not null && multipliers.TryGetValue(level, out var multiplier)
            ? Cp.Of(new BaseStats(form.Attack, form.Defense, form.Stamina), Ivs.Perfect, multiplier)
            : null;

    internal static string? CatchFormId(Form boss) => boss.Id switch
    {
        "necrozma_dawn_wings" or "necrozma_dusk_mane" => "necrozma",
        "kyurem_black" or "kyurem_white" => "kyurem",
        "zacian_crowned_sword" => "zacian_hero",
        "zamazenta_crowned_shield" => "zamazenta_hero",
        _ when boss.Kind is FormKind.Mega or FormKind.Primal => boss.BaseFormId,
        _ => boss.Id,
    };
}
