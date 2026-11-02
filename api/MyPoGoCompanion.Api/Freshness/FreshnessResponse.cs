namespace MyPoGoCompanion.Api.Freshness;

/// <summary>How up to date the app's data is, per kind of data.</summary>
/// <param name="GameData">Pokédex, moves, tier lists, filters: updated by the daily import.</param>
/// <param name="LiveData">Raid bosses, Max Battles, events, eggs and research: updated by the weekly AI researcher.</param>
public sealed record FreshnessResponse(DataFreshness GameData, DataFreshness LiveData);

/// <summary>When one kind of data was last updated, and whether the app should warn that it is out of date.</summary>
/// <param name="LastUpdatedAt">When the last successful run finished; null when it has never run.</param>
/// <param name="DaysSinceUpdate">Whole days since then; null when it has never run.</param>
/// <param name="OutOfDate">
/// True once <paramref name="DaysSinceUpdate"/> reaches the limit for this kind (3 days for game data, 9 for raids and
/// events). Never true for data that has never been updated: there is nothing to be out of date.
/// </param>
/// <param name="LimitDays">The limit for this kind, in days.</param>
public sealed record DataFreshness(DateTimeOffset? LastUpdatedAt, int? DaysSinceUpdate, bool OutOfDate, int LimitDays);
