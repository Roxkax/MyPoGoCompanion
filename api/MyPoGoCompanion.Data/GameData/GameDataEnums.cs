namespace MyPoGoCompanion.Data.GameData;

/// <summary>Legendary, Mythical and Ultra Beast species; everything else is <see cref="None"/>.</summary>
public enum SpeciesCategory
{
    None,
    Legendary,
    Mythical,
    UltraBeast,
}

/// <summary>What a form is to its species. Shadow, Purified, Dynamax and Gigantamax are not forms (same stats): they are flags.</summary>
public enum FormKind
{
    /// <summary>The species' usual form (one per species, the one the Pokédex line shows).</summary>
    Base,

    /// <summary>Alolan, Galarian, Hisuian and Paldean forms.</summary>
    Regional,

    Mega,
    Primal,

    /// <summary>Other battle forms with their own stats or types (Therian, Origin, Crowned, Armored…).</summary>
    Alternate,

    /// <summary>Costumes and event looks: the base form's stats, a different picture.</summary>
    Costume,
}

public enum MoveKind
{
    Fast,
    Charged,
}

/// <summary>How a form gets a move.</summary>
public enum MoveAvailability
{
    /// <summary>Wild, raids, evolving and ordinary TMs.</summary>
    Normal,

    /// <summary>Only from an Elite TM (or an event such as a Community Day).</summary>
    EliteTm,

    /// <summary>Removed from the form's movepool; copies that have it keep it.</summary>
    Legacy,
}
