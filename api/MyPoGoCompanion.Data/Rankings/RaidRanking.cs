using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.Rankings;

/// <summary>
/// A form's raid value for one attacking type: its best moveset of that type at level 40, 15/15/15, against the
/// standard target. Megas are ranked here too; the tier lists keep one Mega slot.
/// </summary>
public class RaidRanking
{
    public required string AttackingTypeId { get; set; }

    public PokemonType AttackingType { get; set; } = null!;

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public bool IsShadow { get; set; }

    /// <summary>Rank within the type, Megas included.</summary>
    public int Rank { get; set; }

    public required string FastMoveId { get; set; }

    public required string ChargedMoveId { get; set; }

    /// <summary>Damage per second.</summary>
    public double Dps { get; set; }

    /// <summary>Total damage output before fainting.</summary>
    public double Tdo { get; set; }

    /// <summary>The raid score (combines DPS and TDO); the screens explain it as damage/s, how long it lasts and % of best.</summary>
    public double Score { get; set; }
}
