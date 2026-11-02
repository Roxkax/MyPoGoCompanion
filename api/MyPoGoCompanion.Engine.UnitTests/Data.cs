using MyPoGoCompanion.Engine.Raids;

namespace MyPoGoCompanion.Engine.UnitTests;

/// <summary>Real game numbers for the tests: PvPoke's CP multiplier table and a few Pokémon and moves.</summary>
internal static class Data
{
    /// <summary>Levels 1 to 51 from PvPoke's <c>Pokemon.js</c> (the golden files' copy).</summary>
    public static CpMultipliers Multipliers { get; } = new(
    [
        0.0939999967813491, 0.135137430784308, 0.166397869586944, 0.192650914456886, 0.215732470154762, 0.236572655026622,
        0.255720049142837, 0.273530381100769, 0.290249884128570, 0.306057381335773, 0.321087598800659, 0.335445032295077,
        0.349212676286697, 0.362457748778790, 0.375235587358474, 0.387592411085168, 0.399567276239395, 0.411193549517250,
        0.422500014305114, 0.432926413410414, 0.443107545375824, 0.453059953871985, 0.462798386812210, 0.472336077786704,
        0.481684952974319, 0.490855810259008, 0.499858438968658, 0.508701756943992, 0.517393946647644, 0.525942508771329,
        0.534354329109191, 0.542635762230353, 0.550792694091796, 0.558830599438087, 0.566754519939422, 0.574569148039264,
        0.582278907299041, 0.589887911977272, 0.597400009632110, 0.604823657502073, 0.612157285213470, 0.619404110566050,
        0.626567125320434, 0.633649181622743, 0.640652954578399, 0.647580963301656, 0.654435634613037, 0.661219263506722,
        0.667934000492096, 0.674581899290818, 0.681164920330047, 0.687684905887771, 0.694143652915954, 0.700542893277978,
        0.706884205341339, 0.713169102333341, 0.719399094581604, 0.725575616972598, 0.731700003147125, 0.734741011137376,
        0.737769484519958, 0.740785574597326, 0.743789434432983, 0.746781208702482, 0.749761044979095, 0.752729105305821,
        0.755685508251190, 0.758630366519684, 0.761563837528228, 0.764486065255226, 0.767397165298461, 0.770297273971590,
        0.773186504840850, 0.776064945942412, 0.778932750225067, 0.781790064808426, 0.784636974334716, 0.787473583646825,
        0.790300011634826, 0.792803950958807, 0.795300006866455, 0.797803921486970, 0.800300002098083, 0.802803892322847,
        0.805299997329711, 0.807803863460723, 0.810299992561340, 0.812803834895026, 0.815299987792968, 0.817803806620319,
        0.820299983024597, 0.822803778631297, 0.825299978256225, 0.827803750922782, 0.830299973487854, 0.832803753381377,
        0.835300028324127, 0.837803755931569, 0.840300023555755, 0.842803729034748, 0.845300018787384,
    ]);

    public static PokemonEntry Charmander { get; } = Entry("charmander", 4, ["fire"], new(116, 93, 118), evolutions: ["charmeleon"]);

    public static PokemonEntry Charmeleon { get; } = Entry("charmeleon", 5, ["fire"], new(158, 126, 151), parent: "charmander", evolutions: ["charizard"]);

    public static PokemonEntry Charizard { get; } = Entry("charizard", 6, ["fire", "flying"], new(223, 173, 186), parent: "charmeleon",
        fast: ["FIRE_SPIN", "WING_ATTACK", "EMBER"], charged: ["BLAST_BURN", "FLAMETHROWER", "DRAGON_CLAW", "RETURN"], elite: ["BLAST_BURN"]);

    public static PokemonEntry CharizardShadow { get; } = Charizard with { Id = "charizard_shadow", IsShadow = true, ChargedMoves = ["BLAST_BURN", "FLAMETHROWER", "FRUSTRATION"] };

    public static PokemonEntry CharizardMegaY { get; } = Entry("charizard_mega_y", 6, ["fire", "flying"], new(319, 212, 186),
        fast: ["FIRE_SPIN"], charged: ["BLAST_BURN"], mega: true) with
    { EliteMoves = ["BLAST_BURN"] };

    public static PokemonEntry Kyogre { get; } = Entry("kyogre", 382, ["water"], new(270, 228, 205),
        fast: ["WATERFALL"], charged: ["ORIGIN_PULSE", "SURF", "BLIZZARD", "THUNDER"]);

    public static PokemonEntry Sceptile { get; } = Entry("sceptile", 254, ["grass"], new(223, 169, 172),
        fast: ["BULLET_SEED", "FURY_CUTTER"], charged: ["FRENZY_PLANT", "LEAF_BLADE", "EARTHQUAKE"], elite: ["FRENZY_PLANT"]);

    public static PokemonEntry SceptileMega { get; } = Entry("sceptile_mega", 254, ["grass", "dragon"], new(320, 186, 172),
        fast: ["BULLET_SEED"], charged: ["FRENZY_PLANT"], mega: true);

    public static PokemonEntry Zekrom { get; } = Entry("zekrom", 644, ["dragon", "electric"], new(275, 211, 225),
        fast: ["CHARGE_BEAM", "DRAGON_BREATH"], charged: ["FUSION_BOLT", "WILD_CHARGE", "OUTRAGE"]);

    public static PokemonEntry Magikarp { get; } = Entry("magikarp", 129, ["water"], new(29, 85, 85), fast: ["SPLASH"], charged: ["STRUGGLE"]);

    public static RaidMoveBook Moves { get; } = new(
    [
        Fast("FIRE_SPIN", "fire", 14, 1100, 9),
        Fast("WING_ATTACK", "flying", 8, 800, 9),
        Fast("EMBER", "fire", 10, 1000, 10),
        Charged("BLAST_BURN", "fire", 120, 3300, 2750, 50),
        Charged("FLAMETHROWER", "fire", 70, 2200, 1700, 50),
        Charged("DRAGON_CLAW", "dragon", 50, 1700, 1500, 33),
        Charged("RETURN", "normal", 35, 700, 500, 33),
        Charged("FRUSTRATION", "normal", 10, 2000, 1500, 33),
        Fast("WATERFALL", "water", 16, 1200, 8),
        Charged("ORIGIN_PULSE", "water", 130, 1700, 1200, 100),
        Charged("SURF", "water", 65, 1700, 1400, 50),
        Charged("BLIZZARD", "ice", 140, 3100, 1500, 100),
        Charged("THUNDER", "electric", 100, 2400, 1200, 100),
        Fast("BULLET_SEED", "grass", 8, 1100, 14),
        Fast("FURY_CUTTER", "bug", 3, 400, 6),
        Charged("FRENZY_PLANT", "grass", 100, 2600, 1500, 50),
        Charged("LEAF_BLADE", "grass", 70, 2400, 1250, 33),
        Charged("EARTHQUAKE", "ground", 140, 3600, 2500, 100),
        Fast("CHARGE_BEAM", "electric", 8, 1100, 15),
        Fast("DRAGON_BREATH", "dragon", 6, 500, 4),
        Charged("FUSION_BOLT", "electric", 140, 2600, 2000, 50),
        Charged("WILD_CHARGE", "electric", 100, 2600, 2000, 50),
        Charged("OUTRAGE", "dragon", 110, 3900, 1500, 50),
        Fast("SPLASH", "water", 0, 1730, 20),
        Charged("STRUGGLE", "normal", 35, 2200, 1200, 0),
    ]);

    public static PokemonEntry Entry(string id, int dex, string[] types, BaseStats stats, string? parent = null,
        string[]? evolutions = null, string[]? fast = null, string[]? charged = null, string[]? elite = null, bool mega = false) => new()
        {
            Id = id,
            Dex = dex,
            Name = id,
            Types = types,
            Stats = stats,
            IsMega = mega,
            Released = true,
            Parent = parent,
            Evolutions = evolutions ?? [],
            FastMoves = fast ?? [],
            ChargedMoves = charged ?? [],
            EliteMoves = elite ?? [],
        };

    private static (string, bool, RaidMove) Fast(string id, string type, double power, int ms, int energy) =>
        (id, true, RaidMove.FromMilliseconds(id, type, power, ms, 0, energy));

    private static (string, bool, RaidMove) Charged(string id, string type, double power, int ms, int window, int cost) =>
        (id, false, RaidMove.FromMilliseconds(id, type, power, ms, window, -cost));
}
