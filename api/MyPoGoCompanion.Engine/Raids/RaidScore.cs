namespace MyPoGoCompanion.Engine.Raids;

/// <summary>An attacker's battle stats: (base + IV) × CP multiplier, with the Shadow bonus; HP rounded down.</summary>
public readonly record struct BattleStats(double Attack, double Defense, int Hp);

/// <summary>Every number the raid score is made of (the "How the raid score works" explainer shows them).</summary>
public sealed record RaidScoreDetail(
    BattleStats Stats,
    double IncomingDps,
    double FastMultiplier,
    double ChargedMultiplier,
    double FastPower,
    double FastDuration,
    int FastEnergy,
    int FastDamage,
    double ChargedPower,
    double ChargedDuration,
    int ChargedCost,
    double ChargedDamageWindow,
    int ChargedDamage,
    double ChargedEnergy,
    double FastDps,
    double FastEps,
    double ChargedDps,
    double ChargedEps,
    double EnergyFromDamage,
    double BaseDps);

/// <summary>One moveset's raid numbers.</summary>
/// <param name="Dps">Damage per second.</param>
/// <param name="Tdo">Total damage before it faints.</param>
/// <param name="Score">(DPS³ × TDO)^¼: damage per second counts three times as much as staying power.</param>
public sealed record RaidScore(string FastMoveId, string ChargedMoveId, double Dps, double Tdo, double Score, RaidScoreDetail Detail)
{
    /// <summary>Seconds it lasts before fainting (HP ÷ damage taken per second).</summary>
    public double Seconds => Detail.Stats.Hp / Detail.IncomingDps;
}

/// <summary>
/// GamePress' "comprehensive DPS" (the Python build's <c>raid_rank.score</c>): the attacker uses fast moves and fires
/// its charged move whenever it has the energy, gains energy from the damage it takes, and fights until it faints.
/// </summary>
public static class RaidFormula
{
    /// <summary>Same-type attack bonus.</summary>
    public const double Stab = 1.2;

    /// <summary>A Mega's bonus to its own types' moves, and to the other types.</summary>
    public const double MegaOwnType = 1.3;

    public const double MegaOtherType = 1.1;

    public const double ShadowAttack = 1.2;

    public const double ShadowDefense = 5.0 / 6;

    /// <summary>The tier lists' attackers: level 40, 15/15/15.</summary>
    public const decimal Level = 40m;

    public const int Iv = 15;

    /// <summary>The tier lists' neutral target: Defense 180, taking 900 ÷ Defense damage per second.</summary>
    public const double NeutralDefense = 180;

    public const double NeutralDamage = 900;

    /// <summary>The attacker's battle stats at <paramref name="cpm"/> with <paramref name="iv"/> in every stat.</summary>
    public static BattleStats Stats(PokemonEntry pokemon, double cpm, int iv = Iv)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var b = pokemon.Stats;
        return new BattleStats(
            (b.Attack + iv) * cpm * (pokemon.IsShadow ? ShadowAttack : 1),
            (b.Defense + iv) * cpm * (pokemon.IsShadow ? ShadowDefense : 1),
            (int)Math.Floor((b.Stamina + iv) * cpm));
    }

    /// <summary>Same-type and Mega bonuses of a move type for this attacker (no type effectiveness).</summary>
    public static double Bonus(PokemonEntry pokemon, string moveType)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var own = pokemon.Types.Contains(moveType, StringComparer.Ordinal);
        var m = own ? Stab : 1.0;
        if (pokemon.IsMega)
        {
            m *= own ? MegaOwnType : MegaOtherType;
        }

        return m;
    }

    /// <summary>Damage of one hit: <c>floor(½ × power × Atk ÷ Def × multiplier) + 1</c>.</summary>
    public static int Damage(double power, double attack, double defense, double multiplier) =>
        (int)Math.Floor(0.5 * power * attack / defense * multiplier) + 1;

    /// <summary>
    /// The raid numbers of one moveset.
    /// </summary>
    /// <param name="fastMultiplier">Every bonus on the fast move (same type, Mega, type effectiveness…).</param>
    /// <param name="targetDefense">The target's Defense (the neutral target's 180, or the boss's).</param>
    /// <param name="incomingDps">Damage the attacker takes per second (the neutral 900 ÷ its Defense, or the boss's).</param>
    public static RaidScore Score(
        BattleStats stats, RaidMove fast, RaidMove charged, double fastMultiplier, double chargedMultiplier,
        double targetDefense, double incomingDps)
    {
        ArgumentNullException.ThrowIfNull(fast);
        ArgumentNullException.ThrowIfNull(charged);
        var y = incomingDps;
        var fdmg = Damage(fast.Power, stats.Attack, targetDefense, fastMultiplier);
        var cdmg = Damage(charged.Power, stats.Attack, targetDefense, chargedMultiplier);
        var fe = fast.Energy;
        var cost = -charged.Energy;

        // A 100-energy move wastes the energy gained past 100 and while it is being used.
        double ce = cost;
        if (cost >= 100)
        {
            ce = cost + (0.5 * fe) + (0.5 * y * charged.DamageWindowStart);
        }

        var fdps = fdmg / fast.Duration;
        var feps = fe / fast.Duration;
        var cdps = cdmg / charged.Duration;
        var ceps = ce / charged.Duration;
        var x = (0.5 * ce) + (0.5 * fe);
        var dps0 = ((fdps * ceps) + (cdps * feps)) / (ceps + feps);
        var dps = dps0 + ((cdps - fdps) / (ceps + feps) * (0.5 - (x / stats.Hp)) * y);
        if (cdps < fdps)
        {
            dps = fdps;
        }

        var tdo = dps * stats.Hp / y;
        var score = Math.Pow(Math.Pow(dps, 3) * tdo, 0.25);
        return new RaidScore(fast.Id, charged.Id, dps, tdo, score, new RaidScoreDetail(
            stats, y, fastMultiplier, chargedMultiplier, fast.Power, fast.Duration, fe, fdmg, charged.Power,
            charged.Duration, cost, charged.DamageWindowStart, cdmg, ce, fdps, feps, cdps, ceps, x, dps0));
    }

    /// <summary>The tier lists' numbers: against the neutral Defense-180 target, taking 900 ÷ Defense per second.</summary>
    public static RaidScore Neutral(PokemonEntry pokemon, BattleStats stats, RaidMove fast, RaidMove charged)
    {
        ArgumentNullException.ThrowIfNull(fast);
        ArgumentNullException.ThrowIfNull(charged);
        return Score(stats, fast, charged, Bonus(pokemon, fast.Type), Bonus(pokemon, charged.Type), NeutralDefense,
            NeutralDamage / stats.Defense);
    }
}
