namespace MyPoGoCompanion.Data.Users;

public enum ChangeKind
{
    New,
    Gone,
    Changed,
}

/// <summary>
/// What an upload changed compared with the one before (matched by family, IVs and catch date): the import report
/// and the "All changes" screen. Kept per upload, so the uploads' history is these diffs.
/// </summary>
public class CollectionChange
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ImportId { get; set; }

    public CollectionImport Import { get; set; } = null!;

    public ChangeKind Kind { get; set; }

    /// <summary>The Pokémon as it is now (null when gone, or once a later upload removed it).</summary>
    public Guid? CollectionPokemonId { get; set; }

    public CollectionPokemon? CollectionPokemon { get; set; }

    /// <summary>The form after the change (before it, when gone).</summary>
    public required string FormId { get; set; }

    /// <summary>What changed for <see cref="ChangeKind.Changed"/>: <c>powered-up</c>, <c>evolved</c>, <c>new-moves</c>…</summary>
    public List<string> What { get; set; } = [];

    /// <summary>The Pokémon's values before (JSON; null when new).</summary>
    public string? Before { get; set; }

    /// <summary>Its values after (JSON; null when gone).</summary>
    public string? After { get; set; }
}
