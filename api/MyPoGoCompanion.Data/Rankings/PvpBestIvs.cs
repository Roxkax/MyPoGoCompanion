using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.Rankings;

/// <summary>
/// A form's rank-1 IVs in a league (highest stat product under the CP cap, level 50). Only rank 1 is stored: the
/// IV rank checker ranks all 4,096 spreads on demand, which takes about a millisecond.
/// </summary>
public class PvpBestIvs
{
    public League League { get; set; }

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public int AttackIv { get; set; }

    public int DefenseIv { get; set; }

    public int StaminaIv { get; set; }

    public decimal Level { get; set; }

    public int Cp { get; set; }

    public double StatProduct { get; set; }
}
