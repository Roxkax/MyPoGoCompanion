using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.Rankings;

/// <summary>
/// How often PvPoke's simulations use a move for a form in a league (decision 2026-11-08): PvPoke's overall rankings
/// list, per ranked form, every fast and charged move with its number of uses. The Pokémon page shows each as a share
/// of its kind (fast or charged).
/// </summary>
public class PvpMoveUsage
{
    public League League { get; set; }

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public bool IsShadow { get; set; }

    public required string MoveId { get; set; }

    public Move Move { get; set; } = null!;

    /// <summary>PvPoke's count of uses in its simulations.</summary>
    public int Uses { get; set; }
}
