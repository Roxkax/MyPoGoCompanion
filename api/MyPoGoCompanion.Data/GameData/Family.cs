namespace MyPoGoCompanion.Data.GameData;

/// <summary>An evolution family sharing one candy (id from the game, e.g. <c>FAMILY_GIBLE</c>).</summary>
public class Family
{
    public required string Id { get; set; }

    public List<Species> Species { get; } = [];
}
