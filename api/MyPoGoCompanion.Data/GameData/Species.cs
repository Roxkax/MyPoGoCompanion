namespace MyPoGoCompanion.Data.GameData;

/// <summary>A Pokédex entry (1–1,025): one line in the Pokédex list. Stats live on its <see cref="Forms"/>.</summary>
public class Species
{
    /// <summary>National Pokédex number.</summary>
    public int Dex { get; set; }

    public required string NameEn { get; set; }

    /// <summary>From the game's Portuguese (pt-BR) text; null until the import finds it (the app falls back to English).</summary>
    public string? NamePt { get; set; }

    public int Generation { get; set; }

    public SpeciesCategory Category { get; set; }

    public required string FamilyId { get; set; }

    public Family Family { get; set; } = null!;

    /// <summary>Common nicknames the search understands ("chomp", "zard", "m2").</summary>
    public List<string> Nicknames { get; set; } = [];

    public List<Form> Forms { get; } = [];
}
