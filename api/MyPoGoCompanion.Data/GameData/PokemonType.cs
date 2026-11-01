namespace MyPoGoCompanion.Data.GameData;

/// <summary>One of the 18 types (id <c>ground</c>, <c>fire</c>…).</summary>
public class PokemonType
{
    public required string Id { get; set; }

    public required string NameEn { get; set; }

    public required string NamePt { get; set; }

    /// <summary>The game's order (Normal first, Fairy last), used by type charts and filters.</summary>
    public int SortOrder { get; set; }
}
