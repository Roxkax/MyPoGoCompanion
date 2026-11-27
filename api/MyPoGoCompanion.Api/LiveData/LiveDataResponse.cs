using MyPoGoCompanion.Data.LiveData;

namespace MyPoGoCompanion.Api.LiveData;

/// <summary>A Pokédex form featured in a live activity.</summary>
/// <param name="Dex">National Pokédex number.</param>
/// <param name="FormId">Form identifier used on the Pokémon page.</param>
/// <param name="NameEn">English form name.</param>
/// <param name="NamePt">Portuguese form name when available.</param>
/// <param name="Types">One or two Pokémon type identifiers.</param>
/// <param name="Sprite">Normal sprite identifier, when available.</param>
/// <param name="SpriteShiny">Shiny sprite identifier, when available.</param>
/// <param name="SpriteGigantamax">Gigantamax sprite identifier, when available.</param>
public sealed record LivePokemon(
    int Dex, string FormId, string NameEn, string? NamePt, IReadOnlyList<string> Types, string? Sprite, string? SpriteShiny, string? SpriteGigantamax);

/// <summary>A schedule expressed in the requested time zone.</summary>
/// <param name="StartsAt">The inclusive start, with its UTC offset.</param>
/// <param name="EndsAt">The exclusive end, with its UTC offset, or null when the end is unknown.</param>
public sealed record LiveSchedule(DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

/// <summary>A reference to a related event.</summary>
public sealed record LiveEventReference(Guid Id, string NameEn, string? NamePt);

/// <summary>Current bosses and all announced future bosses, in chronological order within each list.</summary>
public sealed record RaidRotations(IReadOnlyList<LiveRaidBoss> Now, IReadOnlyList<LiveRaidBoss> Next);

/// <summary>A scheduled raid boss and the perfect CP of its catch encounter, when calculable.</summary>
/// <param name="Id">The rotation entry's identifier.</param>
/// <param name="Pokemon">The form fought in the raid.</param>
/// <param name="Tier">The raid tier.</param>
/// <param name="IsShadow">Whether this is a Shadow raid.</param>
/// <param name="ShinyAvailable">Whether the reward encounter can be shiny.</param>
/// <param name="Schedule">The boss's rotation window.</param>
/// <param name="Event">The related event, if available.</param>
/// <param name="CatchFormId">Pokédex form caught after the raid, or null when unavailable.</param>
/// <param name="PerfectCp">100% IV catch CP at level 20, or null when unavailable.</param>
/// <param name="WeatherBoostedPerfectCp">100% IV weather-boosted catch CP at level 25, or null when unavailable.</param>
public sealed record LiveRaidBoss(
    Guid Id, LivePokemon Pokemon, RaidTier Tier, bool IsShadow, bool ShinyAvailable, LiveSchedule Schedule, LiveEventReference? Event,
    string? CatchFormId, int? PerfectCp, int? WeatherBoostedPerfectCp);

/// <summary>Current Max Battle bosses and all announced future bosses, in chronological order within each list.</summary>
public sealed record MaxBattleRotations(IReadOnlyList<LiveMaxBoss> Now, IReadOnlyList<LiveMaxBoss> Next);

/// <summary>A scheduled Dynamax or Gigantamax boss.</summary>
public sealed record LiveMaxBoss(
    Guid Id, LivePokemon Pokemon, int Stars, bool IsGigantamax, bool ShinyAvailable, LiveSchedule Schedule, LiveEventReference? Event);

/// <summary>An event overlapping the requested calendar days, or starting in the window if its end is unknown.</summary>
public sealed record LiveCalendarEvent(
    Guid Id, EventKind Kind, string NameEn, string? NamePt, string? SummaryEn, string? SummaryPt, LiveSchedule Schedule,
    IReadOnlyList<LiveEventPokemon> Pokemon, IReadOnlyList<LiveEventMove> Moves, IReadOnlyList<LiveEventBonus> Bonuses);

/// <summary>A form featured in an event and whether its shiny is available or boosted.</summary>
public sealed record LiveEventPokemon(LivePokemon Pokemon, bool Shiny);

/// <summary>An event-exclusive move and the instructions for obtaining it, when available.</summary>
public sealed record LiveEventMove(LivePokemon Pokemon, string MoveId, string NameEn, string? NamePt, string? HowEn, string? HowPt);

/// <summary>A bonus listed for the event.</summary>
public sealed record LiveEventBonus(string TextEn, string? TextPt);

/// <summary>A Pokémon currently available from an egg pool.</summary>
/// <param name="Egg">Egg pool kind, including special pools.</param>
/// <param name="Pokemon">The form that hatches from the egg.</param>
/// <param name="ShinyAvailable">Whether it can hatch shiny.</param>
/// <param name="Rarity">Rarity tier from 1 to 5, when known.</param>
/// <param name="Event">The related event for a limited-time entry.</param>
/// <param name="PerfectCp">100% IV hatch CP at level 20, or null when unavailable.</param>
public sealed record LiveEgg(EggKind Egg, LivePokemon Pokemon, bool ShinyAvailable, int? Rarity, LiveEventReference? Event, int? PerfectCp);

/// <summary>A current Field Research task and its Pokémon encounters.</summary>
public sealed record LiveResearchTask(Guid Id, string TaskEn, string? TaskPt, LiveEventReference? Event, IReadOnlyList<LiveResearchReward> Rewards);

/// <summary>A possible Pokémon encounter for a Field Research task.</summary>
/// <param name="Pokemon">The form encountered.</param>
/// <param name="ShinyAvailable">Whether it can be shiny.</param>
/// <param name="PerfectCp">100% IV encounter CP at level 15, or null when unavailable.</param>
public sealed record LiveResearchReward(LivePokemon Pokemon, bool ShinyAvailable, int? PerfectCp);
