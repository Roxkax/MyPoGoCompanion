namespace MyPoGoCompanion.Data.GameData;

/// <summary>
/// A fast or charged move (id from the game, e.g. <c>EARTH_POWER</c>). The game uses different numbers in raids
/// (PvE) and in trainer battles (PvP), so both sets are kept.
/// </summary>
public class Move
{
    public required string Id { get; set; }

    public MoveKind Kind { get; set; }

    public required string TypeId { get; set; }

    public PokemonType Type { get; set; } = null!;

    public required string NameEn { get; set; }

    public string? NamePt { get; set; }

    public double RaidPower { get; set; }

    public int RaidDurationMs { get; set; }

    public int RaidDamageWindowStartMs { get; set; }

    public int RaidDamageWindowEndMs { get; set; }

    /// <summary>Energy gained (fast, positive) or spent (charged, negative).</summary>
    public int RaidEnergyDelta { get; set; }

    public double PvpPower { get; set; }

    public int PvpEnergyDelta { get; set; }

    /// <summary>Fast moves only: turns of 0.5 s.</summary>
    public int? PvpTurns { get; set; }

    /// <summary>Chance (0–1) that the stat changes below happen; null when the move has none.</summary>
    public double? PvpBuffChance { get; set; }

    public int PvpSelfAttackStages { get; set; }

    public int PvpSelfDefenseStages { get; set; }

    public int PvpOpponentAttackStages { get; set; }

    public int PvpOpponentDefenseStages { get; set; }
}
