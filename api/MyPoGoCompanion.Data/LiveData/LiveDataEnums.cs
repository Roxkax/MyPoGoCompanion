namespace MyPoGoCompanion.Data.LiveData;

public enum RaidTier
{
    OneStar,
    ThreeStar,
    FiveStar,
    Mega,

    /// <summary>Legendary Megas and Primals.</summary>
    LegendaryMega,
    Elite,
}

public enum EventKind
{
    CommunityDay,
    SpotlightHour,
    RaidHour,
    RaidDay,
    MaxBattle,
    Season,
    Special,
}

public enum EggKind
{
    TwoKm,
    FiveKm,
    SevenKm,
    TenKm,
    TwelveKm,
    AdventureSyncFiveKm,
    AdventureSyncTenKm,
    Route,
}
