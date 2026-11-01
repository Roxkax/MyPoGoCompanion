namespace MyPoGoCompanion.Data.Users;

/// <summary>A row of an upload that couldn't be matched to the Pokédex, with the reason shown in the report.</summary>
public class UnrecognisedRow
{
    public Guid ImportId { get; set; }

    public CollectionImport Import { get; set; } = null!;

    /// <summary>Row number in the file (1 = first Pokémon).</summary>
    public int RowNumber { get; set; }

    public required string Name { get; set; }

    public string? Form { get; set; }

    public int? Cp { get; set; }

    public required string Reason { get; set; }
}
