namespace MyPoGoCompanion.Data.Rankings;

/// <summary>
/// The appraisal bars to keep for a Pokédex number in a league (bars 0–4 per stat): the keeper filters' box,
/// judged by the forms in <see cref="JudgedBy"/> (its evolutions when they are better).
/// </summary>
public class PvpKeeperBox
{
    public League League { get; set; }

    public int Dex { get; set; }

    public int AttackBarMin { get; set; }

    public int AttackBarMax { get; set; }

    public int DefenseBarMin { get; set; }

    public int DefenseBarMax { get; set; }

    public int StaminaBarMin { get; set; }

    public int StaminaBarMax { get; set; }

    /// <summary>Form ids whose IV ranks chose the box.</summary>
    public List<string> JudgedBy { get; set; } = [];
}
