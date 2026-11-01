using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.Rankings;

/// <summary>One fast + charged combination of a form, ranked for raids (the Pokémon page's moveset ranking).</summary>
public class RaidMoveset
{
    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public bool IsShadow { get; set; }

    public required string FastMoveId { get; set; }

    public required string ChargedMoveId { get; set; }

    /// <summary>1 = the form's best combination.</summary>
    public int Rank { get; set; }

    public double Dps { get; set; }

    public double Tdo { get; set; }

    public double Score { get; set; }
}
