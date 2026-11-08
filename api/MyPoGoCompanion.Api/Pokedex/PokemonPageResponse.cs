using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Rankings;

namespace MyPoGoCompanion.Api.Pokedex;

/// <summary>
/// Everything the Pokémon page (B2) shows for one species, all its versions included, in one answer (decision
/// 2026-11-08): switching versions or tabs needs no new request. The IV rank checker works in the app from the base
/// stats and <see cref="CpMultipliers"/>.
/// </summary>
/// <param name="Dex">National Pokédex number.</param>
/// <param name="NameEn">The species' English name.</param>
/// <param name="NamePt">The game's Portuguese name; null when missing (the app shows the English one).</param>
/// <param name="Generation">1 (Kanto) to 9 (Paldea).</param>
/// <param name="Category">Legendary, Mythical, Ultra Beast, or None.</param>
/// <param name="FamilyId">Its evolution family.</param>
/// <param name="Forms">Its versions: the main form first, then Megas and Primals, regional forms, other forms and looks.</param>
/// <param name="KeeperBoxes">The appraisal bars the keeper filters keep for this Pokédex number (Great and Ultra League).</param>
/// <param name="Moves">Every move its versions can learn or use, with raid and PvP numbers.</param>
/// <param name="Family">Every version of every species in its family (Megas left out), for the evolution chain and the IV checker.</param>
/// <param name="Evolutions">The family's evolution steps with what each needs.</param>
/// <param name="Costumes">Its event costumes (from PokeMiners' pictures; no dates).</param>
/// <param name="Appearances">
/// Where its versions appear or appeared (raids, Max Battles, eggs, research, events), from the live data; the app
/// tells "right now" from history with its own clock.
/// </param>
/// <param name="CpMultipliers">The game's CP multiplier per half level from level 1.</param>
public sealed record PokemonPageResponse(
    int Dex,
    string NameEn,
    string? NamePt,
    int Generation,
    SpeciesCategory Category,
    string FamilyId,
    IReadOnlyList<PageForm> Forms,
    IReadOnlyList<PageKeeperBox> KeeperBoxes,
    IReadOnlyList<PageMove> Moves,
    IReadOnlyList<FamilyMember> Family,
    IReadOnlyList<PageEvolution> Evolutions,
    IReadOnlyList<PageCostume> Costumes,
    IReadOnlyList<PageAppearance> Appearances,
    IReadOnlyList<double> CpMultipliers);

/// <summary>A version of the species with everything the page shows about it.</summary>
/// <param name="Id">PvPoke's id (<c>garchomp_mega</c>); the page's <c>?version=</c>.</param>
/// <param name="Kind">Base, Regional, Mega, Primal, Alternate or Costume.</param>
/// <param name="IsDefault">The form the page shows first (exactly one per species).</param>
/// <param name="NameEn">The name to show ("Mega Garchomp").</param>
/// <param name="NamePt">The game's Portuguese name; null when missing.</param>
/// <param name="LabelEn">Its short label on the version switcher ("Mega"); null for the main form.</param>
/// <param name="LabelPt">The label in Portuguese; null when missing.</param>
/// <param name="Types">One or two type ids.</param>
/// <param name="Attack">Base Attack.</param>
/// <param name="Defense">Base Defense.</param>
/// <param name="Stamina">Base Stamina.</param>
/// <param name="MaxCp">Max CP with 15/15/15 at levels 40, 50 and 51.</param>
/// <param name="PerfectCp">The 100% CP cheat sheet: 15/15/15 at levels 15, 20, 25, 30 and 35.</param>
/// <param name="Released">Available in Pokémon GO.</param>
/// <param name="Shadow">Its Shadow (and so Purified) version is released.</param>
/// <param name="Shiny">Its shiny is released; null while unknown (the AI researcher fills it).</param>
/// <param name="Dynamax">It can Dynamax.</param>
/// <param name="Gigantamax">It can Gigantamax.</param>
/// <param name="Sprite">Its picture's name; null when none.</param>
/// <param name="SpriteShiny">Its shiny picture's name; null when none.</param>
/// <param name="SpriteGigantamax">Its Gigantamax picture's name; null when none.</param>
/// <param name="LooksOnly">Same stats and types as the main form, only a different look.</param>
/// <param name="BuddyDistanceKm">Walking distance per candy as a buddy.</param>
/// <param name="SecondMoveStardust">Stardust to unlock a second charged move.</param>
/// <param name="Movepool">Every move it can have and how (Normal, EliteTm, Legacy).</param>
/// <param name="Matchups">The damage multiplier of each attacking type against it (GO's chart, no immunities).</param>
/// <param name="Pvp">PvPoke's ranking per league (and its Shadow), with the recommended moveset and move usage.</param>
/// <param name="BestIvs">The rank-1 IVs per league (a tie goes to the higher CP).</param>
/// <param name="RaidRanks">Its rank in each attacking type's raid list (and its Shadow's).</param>
/// <param name="RaidMovesets">Every fast + charged combination ranked for raids (and for its Shadow).</param>
public sealed record PageForm(
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
    IReadOnlyList<LevelCpRow> MaxCp,
    IReadOnlyList<LevelCpRow> PerfectCp,
    bool Released,
    bool Shadow,
    bool? Shiny,
    bool Dynamax,
    bool Gigantamax,
    string? Sprite,
    string? SpriteShiny,
    string? SpriteGigantamax,
    bool LooksOnly,
    int BuddyDistanceKm,
    int SecondMoveStardust,
    IReadOnlyList<PageFormMove> Movepool,
    IReadOnlyList<TypeMultiplier> Matchups,
    IReadOnlyList<PagePvpRanking> Pvp,
    IReadOnlyList<PageBestIvs> BestIvs,
    IReadOnlyList<PageRaidRank> RaidRanks,
    IReadOnlyList<PageRaidMoveset> RaidMovesets);

/// <summary>A CP at a level.</summary>
public sealed record LevelCpRow(decimal Level, int Cp);

/// <summary>A move in a form's movepool.</summary>
public sealed record PageFormMove(string MoveId, MoveAvailability Availability);

/// <summary>How much damage an attacking type does to the form (×1.6, ×2.56, ×0.625, ×0.39…).</summary>
public sealed record TypeMultiplier(string Type, double Multiplier);

/// <summary>PvPoke's ranking of the form (or its Shadow) in a league.</summary>
/// <param name="League">Great, Ultra or Master.</param>
/// <param name="IsShadow">The Shadow version's ranking.</param>
/// <param name="Rank">Its place in PvPoke's overall ranking.</param>
/// <param name="Score">PvPoke's score (0–100).</param>
/// <param name="FastMoveId">The recommended fast move.</param>
/// <param name="ChargedMove1Id">The recommended first charged move.</param>
/// <param name="ChargedMove2Id">The recommended second charged move; null when it has only one.</param>
/// <param name="Usage">How often PvPoke's simulations use each of its moves (fast and charged together; the app splits them).</param>
public sealed record PagePvpRanking(
    League League,
    bool IsShadow,
    int Rank,
    double Score,
    string FastMoveId,
    string ChargedMove1Id,
    string? ChargedMove2Id,
    IReadOnlyList<MoveUses> Usage);

/// <summary>A move's number of uses in PvPoke's simulations.</summary>
public sealed record MoveUses(string MoveId, int Uses);

/// <summary>The rank-1 IVs in a league.</summary>
public sealed record PageBestIvs(League League, int Attack, int Defense, int Stamina, decimal Level, int Cp);

/// <summary>The bars (0–4 each) the keeper filters keep in a league.</summary>
public sealed record PageKeeperBox(League League, int AttackMin, int AttackMax, int DefenseMin, int DefenseMax, int StaminaMin, int StaminaMax);

/// <summary>The form's place in an attacking type's raid list, with the moveset it is ranked with.</summary>
public sealed record PageRaidRank(string Type, bool IsShadow, int Rank, string FastMoveId, string ChargedMoveId);

/// <summary>A fast + charged combination ranked for raids.</summary>
public sealed record PageRaidMoveset(bool IsShadow, string FastMoveId, string ChargedMoveId, int Rank, double Dps, double Tdo, double Score);

/// <summary>A move with its raid and PvP numbers.</summary>
/// <param name="Id">The game's id.</param>
/// <param name="NameEn">English name.</param>
/// <param name="NamePt">The game's Portuguese name; null when missing.</param>
/// <param name="Type">Its type id.</param>
/// <param name="Kind">Fast or Charged.</param>
/// <param name="RaidPower">Raid power.</param>
/// <param name="RaidDurationMs">Raid duration in milliseconds; 0 when it has no raid version.</param>
/// <param name="RaidEnergy">Raid energy gained (fast) or spent (charged, negative).</param>
/// <param name="PvpPower">PvP power.</param>
/// <param name="PvpEnergy">PvP energy gained (fast) or spent (charged, negative).</param>
/// <param name="PvpTurns">PvP turns of a fast move; null for charged moves.</param>
/// <param name="PvpBuffChance">Chance (0–1) of its stat changes; null when none.</param>
/// <param name="PvpSelfAttack">Stages added to the user's Attack.</param>
/// <param name="PvpSelfDefense">Stages added to the user's Defense.</param>
/// <param name="PvpOpponentAttack">Stages added to the opponent's Attack.</param>
/// <param name="PvpOpponentDefense">Stages added to the opponent's Defense.</param>
public sealed record PageMove(
    string Id,
    string NameEn,
    string? NamePt,
    string Type,
    MoveKind Kind,
    double RaidPower,
    int RaidDurationMs,
    int RaidEnergy,
    double PvpPower,
    int PvpEnergy,
    int? PvpTurns,
    double? PvpBuffChance,
    int PvpSelfAttack,
    int PvpSelfDefense,
    int PvpOpponentAttack,
    int PvpOpponentDefense);

/// <summary>A version of a species in the family, for the evolution chain and the IV checker.</summary>
public sealed record FamilyMember(
    int Dex,
    string FormId,
    FormKind Kind,
    bool IsDefault,
    string NameEn,
    string? NamePt,
    string SpeciesNameEn,
    IReadOnlyList<string> Types,
    int Attack,
    int Defense,
    int Stamina,
    bool Released,
    string? Sprite);

/// <summary>An evolution step and what it needs.</summary>
/// <param name="FromFormId">The form that evolves.</param>
/// <param name="ToFormId">What it becomes.</param>
/// <param name="Candy">Candy needed.</param>
/// <param name="CandyAfterTrade">Candy needed when traded first (free evolutions are 0); null when no discount.</param>
/// <param name="ItemId">An item needed (<c>ITEM_SUN_STONE</c>); null when none.</param>
/// <param name="BuddyDistanceKm">Kilometres walked as a buddy; null when not needed.</param>
/// <param name="TimeOfDay"><c>day</c> or <c>night</c>; null when any time.</param>
/// <param name="LureId">A lure module needed; null when none.</param>
/// <param name="Gender"><c>male</c> or <c>female</c>; null when either.</param>
/// <param name="TaskEn">A special task in English; null when none.</param>
/// <param name="TaskPt">The task in Portuguese; null when missing.</param>
public sealed record PageEvolution(
    string FromFormId,
    string ToFormId,
    int Candy,
    int? CandyAfterTrade,
    string? ItemId,
    int? BuddyDistanceKm,
    string? TimeOfDay,
    string? LureId,
    string? Gender,
    string? TaskEn,
    string? TaskPt);

/// <summary>An event costume: its picture's name and a readable name ("Holiday 2022").</summary>
public sealed record PageCostume(string Sprite, string NameEn, string? FormCode);

/// <summary>Where an appearance is.</summary>
public enum AppearanceKind
{
    Raid,
    MaxBattle,
    Egg,
    Research,
    Event,
}

/// <summary>
/// One appearance of a version in the live data. Times are the game's local wall-clock times (null when the source
/// gave none); the app compares them with its own clock.
/// </summary>
/// <param name="Kind">Raid, MaxBattle, Egg, Research or Event.</param>
/// <param name="FormId">The version that appears.</param>
/// <param name="IsShadow">A Shadow raid.</param>
/// <param name="RaidTier">The raid's tier (raids only).</param>
/// <param name="MaxStars">The Max Battle's stars (Max Battles only).</param>
/// <param name="Gigantamax">A Gigantamax Max Battle.</param>
/// <param name="Egg">The egg (eggs only).</param>
/// <param name="TaskEn">The research task (research only).</param>
/// <param name="TaskPt">The research task in Portuguese; null when missing.</param>
/// <param name="EventNameEn">The event it belongs to (or the event itself); null when none.</param>
/// <param name="EventNamePt">The event's Portuguese name; null when missing.</param>
/// <param name="EventKind">The event's kind; null when no event.</param>
/// <param name="StartsAt">When it starts (local time); null when not known.</param>
/// <param name="EndsAt">When it ends (local time); null when open-ended.</param>
/// <param name="Shiny">Its shiny can be found there.</param>
public sealed record PageAppearance(
    AppearanceKind Kind,
    string FormId,
    bool IsShadow,
    RaidTier? RaidTier,
    int? MaxStars,
    bool Gigantamax,
    EggKind? Egg,
    string? TaskEn,
    string? TaskPt,
    string? EventNameEn,
    string? EventNamePt,
    EventKind? EventKind,
    DateTime? StartsAt,
    DateTime? EndsAt,
    bool Shiny);
