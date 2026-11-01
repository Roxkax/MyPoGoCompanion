namespace MyPoGoCompanion.Data.GameData;

/// <summary>
/// A version of a species with its own stats, types or picture: the base form, regional forms, Megas, Primals,
/// other battle forms and costumes. The id is PvPoke's species id (<c>garchomp</c>, <c>garchomp_mega</c>,
/// <c>vulpix_alolan</c>). Shadow, Purified, Dynamax and Gigantamax share the form's stats, so they are flags here
/// and an <c>is_shadow</c> column where rankings differ.
/// </summary>
public class Form
{
    public required string Id { get; set; }

    public int Dex { get; set; }

    public Species Species { get; set; } = null!;

    public FormKind Kind { get; set; }

    /// <summary>True for exactly one form per species: the one the Pokédex line shows.</summary>
    public bool IsDefault { get; set; }

    /// <summary>The game master's name for it (<c>GARCHOMP</c>, <c>VULPIX_ALOLA</c>), used to match other sources.</summary>
    public string? GameMasterId { get; set; }

    public required string NameEn { get; set; }

    public string? NamePt { get; set; }

    public required string Type1Id { get; set; }

    public PokemonType Type1 { get; set; } = null!;

    public string? Type2Id { get; set; }

    public PokemonType? Type2 { get; set; }

    public int Attack { get; set; }

    public int Defense { get; set; }

    public int Stamina { get; set; }

    /// <summary>15/15/15 at level 50, stored so the Pokédex can filter and sort by it.</summary>
    public int MaxCp50 { get; set; }

    /// <summary>For a Mega, Primal or costume: the form it comes from.</summary>
    public string? BaseFormId { get; set; }

    public Form? BaseForm { get; set; }

    /// <summary>Available in GO (false for announced or datamined only).</summary>
    public bool Released { get; set; }

    public bool ShadowReleased { get; set; }

    /// <summary>Null when no source says yet.</summary>
    public bool? ShinyReleased { get; set; }

    public bool CanDynamax { get; set; }

    public bool CanGigantamax { get; set; }

    public int BuddyDistanceKm { get; set; }

    /// <summary>Stardust for the second charged move (the candy cost follows from the buddy distance).</summary>
    public int ThirdMoveStardust { get; set; }

    public List<FormMove> Moves { get; } = [];
}
