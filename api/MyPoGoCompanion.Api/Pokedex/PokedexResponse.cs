using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Api.Pokedex;

/// <summary>
/// The whole Pokédex in one answer (decision 2026-11-08): every species with its versions and the moves they learn,
/// only the fields the Pokédex list (B1) and its search use. The app searches, filters and sorts it itself.
/// </summary>
/// <param name="Species">Every species, in Pokédex order.</param>
/// <param name="Moves">Every move, for searching by move (<c>@earthquake</c>) and by move type (<c>@fire</c>).</param>
public sealed record PokedexResponse(IReadOnlyList<PokedexSpecies> Species, IReadOnlyList<PokedexMove> Moves);

/// <summary>One Pokédex entry: one line of the list.</summary>
/// <param name="Dex">National Pokédex number.</param>
/// <param name="NameEn">The species' English name.</param>
/// <param name="NamePt">The game's Portuguese name; null when the game text lacks it (the app shows the English one).</param>
/// <param name="Generation">1 (Kanto) to 9 (Paldea), by Pokédex number.</param>
/// <param name="Category">Legendary, Mythical, Ultra Beast, or None.</param>
/// <param name="FamilyId">The evolution family (<c>+pikachu</c> finds the whole family).</param>
/// <param name="Nicknames">Common nicknames the search understands (<c>chomp</c>, <c>zard</c>, <c>m2</c>).</param>
/// <param name="Forms">
/// Its versions, the main form first, then Megas and Primals, regional forms, other forms and looks-only forms.
/// </param>
public sealed record PokedexSpecies(
    int Dex,
    string NameEn,
    string? NamePt,
    int Generation,
    SpeciesCategory Category,
    string FamilyId,
    IReadOnlyList<string> Nicknames,
    IReadOnlyList<PokedexForm> Forms);

/// <summary>A version of a species with its own stats, types or picture.</summary>
/// <param name="Id">PvPoke's id (<c>charizard_mega_y</c>); the Pokémon page's <c>?version=</c>.</param>
/// <param name="Kind">Base, Regional, Mega, Primal, Alternate or Costume.</param>
/// <param name="IsDefault">The form the line shows when nothing else is asked for (exactly one per species).</param>
/// <param name="NameEn">The name to show ("Mega Charizard Y", "Hisuian Arcanine", "Giratina (Origin)").</param>
/// <param name="NamePt">The game's Portuguese name ("Mega Charizard Y", "Arcanine de Hisui"); null when missing.</param>
/// <param name="LabelEn">The short text of its marker ("Mega Y", "Hisuian", "Origin"); null for the main form.</param>
/// <param name="LabelPt">The marker's text in Portuguese ("Mega Y", "Hisui", "Forma Origem"); null when missing.</param>
/// <param name="Types">One or two type ids (<c>fire</c>, <c>flying</c>).</param>
/// <param name="Attack">Base Attack.</param>
/// <param name="Defense">Base Defense.</param>
/// <param name="Stamina">Base Stamina.</param>
/// <param name="MaxCp">Max CP at level 50 with 15/15/15.</param>
/// <param name="Released">Available in Pokémon GO.</param>
/// <param name="Shadow">Its Shadow version is released.</param>
/// <param name="Dynamax">It can Dynamax.</param>
/// <param name="Gigantamax">It can Gigantamax.</param>
/// <param name="Sprite">Its picture's name (<c>pm6.fMEGA_Y</c>, served as <c>/sprites/pm6.fMEGA_Y.webp</c>); null when none.</param>
/// <param name="SpriteGigantamax">Its Gigantamax picture's name; null when it can't Gigantamax or has none.</param>
/// <param name="LooksOnly">
/// Same stats and types as the main form, only a different look (costumes, Burmy's cloaks, Genesect's drives): no
/// marker on the list (decision 2026-11-08).
/// </param>
/// <param name="FastMoves">Ids of every fast move it can have (Elite TM and Legacy included).</param>
/// <param name="ChargedMoves">Ids of every charged move it can have (Elite TM and Legacy included).</param>
public sealed record PokedexForm(
    string Id,
    FormKind Kind,
    bool IsDefault,
    string NameEn,
    string? NamePt,
    string? LabelEn,
    string? LabelPt,
    IReadOnlyList<string> Types,
    int Attack,
    int Defense,
    int Stamina,
    int MaxCp,
    bool Released,
    bool Shadow,
    bool Dynamax,
    bool Gigantamax,
    string? Sprite,
    string? SpriteGigantamax,
    bool LooksOnly,
    IReadOnlyList<string> FastMoves,
    IReadOnlyList<string> ChargedMoves);

/// <summary>A move, for the Pokédex search.</summary>
/// <param name="Id">The game's id (<c>EARTHQUAKE</c>).</param>
/// <param name="NameEn">English name.</param>
/// <param name="NamePt">The game's Portuguese name; null when missing.</param>
/// <param name="Type">Its type id (<c>ground</c>).</param>
/// <param name="Kind">Fast or Charged.</param>
public sealed record PokedexMove(string Id, string NameEn, string? NamePt, string Type, MoveKind Kind);
