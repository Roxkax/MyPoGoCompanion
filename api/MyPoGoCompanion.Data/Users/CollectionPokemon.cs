using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.Users;

public enum Alignment
{
    Normal,
    Shadow,
    Purified,
}

/// <summary>
/// One Pokémon of a user's current collection, as the last Poke Genie upload says (the upload is the source of
/// truth: a new upload replaces the collection).
/// </summary>
public class CollectionPokemon
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>The upload it last came from.</summary>
    public Guid ImportId { get; set; }

    public CollectionImport Import { get; set; } = null!;

    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public Alignment Alignment { get; set; }

    public string? Nickname { get; set; }

    public int Cp { get; set; }

    public int AttackIv { get; set; }

    public int DefenseIv { get; set; }

    public int StaminaIv { get; set; }

    /// <summary>1–51 in steps of 0.5.</summary>
    public decimal Level { get; set; }

    public string? FastMoveId { get; set; }

    public Move? FastMove { get; set; }

    public string? ChargedMove1Id { get; set; }

    public Move? ChargedMove1 { get; set; }

    public string? ChargedMove2Id { get; set; }

    public Move? ChargedMove2 { get; set; }

    public bool IsShiny { get; set; }

    public bool IsLucky { get; set; }

    public bool IsFavourite { get; set; }

    /// <summary><c>male</c>, <c>female</c> or null (genderless or unknown).</summary>
    public string? Gender { get; set; }

    public DateOnly? CaughtOn { get; set; }
}
