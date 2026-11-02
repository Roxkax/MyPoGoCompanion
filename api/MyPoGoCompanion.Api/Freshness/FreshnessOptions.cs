namespace MyPoGoCompanion.Api.Freshness;

/// <summary>
/// When data counts as out of date, from the <c>Freshness</c> configuration section (environment variables
/// <c>Freshness__…</c>). Defaults from spec 16.3: 3 days without a daily game-data update, 9 days without a raids and
/// events update.
/// </summary>
public sealed class FreshnessOptions
{
    public const string Section = "Freshness";

    /// <summary>Days without a successful daily import before game data is out of date (<c>Freshness__GameDataDays</c>).</summary>
    public int GameDataDays { get; set; } = 3;

    /// <summary>Days without a successful AI researcher run before raids and events are out of date (<c>Freshness__LiveDataDays</c>).</summary>
    public int LiveDataDays { get; set; } = 9;
}
