using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.Rankings;

/// <summary>A form's place in a league's PvPoke ranking, with the moveset PvPoke recommends.</summary>
public class PvpRanking
{
    public League League { get; set; }

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public bool IsShadow { get; set; }

    public int Rank { get; set; }

    /// <summary>PvPoke's score (0–100); the tier lists group it into S/A/B/C.</summary>
    public double Score { get; set; }

    public required string FastMoveId { get; set; }

    public required string ChargedMove1Id { get; set; }

    public string? ChargedMove2Id { get; set; }
}
