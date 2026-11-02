using MyPoGoCompanion.Engine.Types;

namespace MyPoGoCompanion.Engine.Raids;

/// <summary>Raid tiers, for the boss's CP multiplier.</summary>
public enum BossTier
{
    OneStar,
    ThreeStar,
    FiveStar,
    Mega,
    LegendaryMega,
    Elite,
}

/// <summary>A raid boss: its entry (a Shadow boss is a <c>_shadow</c> entry), tier and possible moves.</summary>
public sealed record RaidBossSetup(PokemonEntry Boss, BossTier Tier, IReadOnlyList<RaidMove> FastMoves, IReadOnlyList<RaidMove> ChargedMoves);

/// <summary>What a counter does against one of the boss's movesets.</summary>
public sealed record CounterAgainst(string BossFastMoveId, string BossChargedMoveId, RaidScore Score);

/// <summary>A counter with its best moveset against the boss, averaged over the boss's movesets being considered.</summary>
/// <param name="Dps">Average damage per second.</param>
/// <param name="Seconds">Average seconds it lasts before fainting.</param>
/// <param name="Score">Average raid score: the counters are ranked by it.</param>
/// <param name="ShareOfBest">Score as a share of the best counter's (1 = the best).</param>
public sealed record Counter(
    PokemonEntry Pokemon, string FastMoveId, string ChargedMoveId, bool EliteFast, bool EliteCharged,
    double Dps, double Seconds, double Tdo, double Score, double ShareOfBest, IReadOnlyList<CounterAgainst> PerBossMoveset);

/// <summary>
/// Counters against a specific raid boss (decision 2026-11-02). The boss's Attack and Defense are (base + 15) × its
/// tier's CP multiplier (with the Shadow bonus for a Shadow boss). The counter's hits count its type effectiveness
/// against the boss's types, same type, Shadow and Mega; the damage it takes comes from the boss's real moves and
/// its own types, the boss using its fast move every move length + 2 s and its charged move whenever it has the
/// energy. That replaces the tier lists' neutral target in the same raid formula. With no moves picked, every fast +
/// charged combination of the boss counts equally; counters are level 40, 15/15/15, released, Shadows included, no
/// weather, friendship or dodging.
/// </summary>
public sealed class BossCounters
{
    /// <summary>Seconds a boss waits between its attacks, on top of the move's own length.</summary>
    public const double BossDelay = 2.0;

    /// <summary>How many counters the raid boss page shows.</summary>
    public const int Shown = 9;

    private readonly IReadOnlyList<PokemonEntry> _counters;
    private readonly RaidMoveBook _moves;
    private readonly double _cpm;

    /// <param name="entries">All entries (PvPoke's order); released ones, battle-only forms aside, are the counters.</param>
    public BossCounters(IEnumerable<PokemonEntry> entries, RaidMoveBook moves, CpMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(multipliers);
        _moves = moves ?? throw new ArgumentNullException(nameof(moves));
        _counters = [.. entries.Where(p => p.Released && !PokemonEntry.BattleOnly.Contains(p.Id))];
        _cpm = multipliers.At(RaidFormula.Level);
    }

    /// <summary>
    /// The boss's CP multiplier per tier (the game's raid boss values; every boss has 15/15/15).
    /// </summary>
    public static double TierMultiplier(BossTier tier) => tier switch
    {
        BossTier.OneStar => 0.5974,
        BossTier.ThreeStar => 0.73,
        BossTier.FiveStar or BossTier.Mega or BossTier.LegendaryMega or BossTier.Elite => 0.79,
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown raid tier."),
    };

    /// <summary>The boss's battle Attack and Defense (its HP doesn't change the ranking).</summary>
    public static BattleStats BossStats(RaidBossSetup boss)
    {
        ArgumentNullException.ThrowIfNull(boss);
        return RaidFormula.Stats(boss.Boss, TierMultiplier(boss.Tier));
    }

    /// <summary>
    /// The damage per second the boss deals to a defender using one fast + charged moveset: for every charged move,
    /// enough fast moves to fill its energy, each followed by the boss's 2-second wait.
    /// </summary>
    public static double BossDps(RaidBossSetup boss, RaidMove fast, RaidMove charged, BattleStats defender, IReadOnlyList<string> defenderTypes)
    {
        ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(fast);
        ArgumentNullException.ThrowIfNull(charged);
        var attack = BossStats(boss).Attack;
        var fastDamage = RaidFormula.Damage(fast.Power, attack, defender.Defense, BossMultiplier(boss.Boss, fast.Type, defenderTypes));
        var chargedDamage = RaidFormula.Damage(charged.Power, attack, defender.Defense, BossMultiplier(boss.Boss, charged.Type, defenderTypes));
        var cost = Math.Max(1, -charged.Energy);
        var fastPerCharged = fast.Energy > 0 ? Math.Ceiling(cost / (double)fast.Energy) : double.PositiveInfinity;
        if (double.IsPositiveInfinity(fastPerCharged))
        {
            return fastDamage / (fast.Duration + BossDelay);
        }

        var damage = (fastPerCharged * fastDamage) + chargedDamage;
        var time = (fastPerCharged * (fast.Duration + BossDelay)) + charged.Duration + BossDelay;
        return damage / time;
    }

    /// <summary>
    /// The counters against the boss, best first. <paramref name="bossFast"/> / <paramref name="bossCharged"/> pick
    /// the boss's actual moves (null: all of its moves count equally).
    /// </summary>
    public IReadOnlyList<Counter> Rank(RaidBossSetup boss, string? bossFast = null, string? bossCharged = null)
    {
        ArgumentNullException.ThrowIfNull(boss);
        var bossMovesets = (
            from f in boss.FastMoves
            where bossFast is null || f.Id == bossFast
            from c in boss.ChargedMoves
            where bossCharged is null || c.Id == bossCharged
            select (Fast: f, Charged: c)).ToList();
        if (bossMovesets.Count == 0)
        {
            throw new ArgumentException("The boss has no moveset with the moves picked.", nameof(boss));
        }

        var bossStats = BossStats(boss);
        var counters = new List<Counter>();
        foreach (var p in _counters)
        {
            var stats = RaidFormula.Stats(p, _cpm);
            var types = p.Types;
            var incoming = bossMovesets.Select(m => BossDps(boss, m.Fast, m.Charged, stats, types)).ToList();
            Counter? best = null;
            foreach (var f in Moves(p.FastMoves, true))
            {
                var fastMultiplier = Multiplier(p, f.Type, boss.Boss.Types);
                foreach (var c in Moves(p.ChargedMoves, false))
                {
                    var chargedMultiplier = Multiplier(p, c.Type, boss.Boss.Types);
                    var per = bossMovesets.Select((m, i) => new CounterAgainst(m.Fast.Id, m.Charged.Id,
                        RaidFormula.Score(stats, f, c, fastMultiplier, chargedMultiplier, bossStats.Defense, incoming[i]))).ToList();
                    var score = per.Average(x => x.Score.Score);
                    if (best is null || score > best.Score)
                    {
                        var elite = p.EliteMoves.ToHashSet(StringComparer.Ordinal);
                        best = new Counter(p, f.Id, c.Id, elite.Contains(f.Id), elite.Contains(c.Id),
                            per.Average(x => x.Score.Dps), per.Average(x => x.Score.Seconds), per.Average(x => x.Score.Tdo),
                            score, 0, per);
                    }
                }
            }

            if (best is not null)
            {
                counters.Add(best);
            }
        }

        var ordered = counters.OrderByDescending(c => c.Score).ToList();
        var top = ordered.Count > 0 ? ordered[0].Score : 1;
        return [.. ordered.Select(c => c with { ShareOfBest = c.Score / top })];
    }

    /// <summary>The first <paramref name="count"/> counters with at most one Mega or Primal (the page's one Mega slot).</summary>
    public static IReadOnlyList<Counter> WithOneMega(IEnumerable<Counter> ranked, int count = Shown)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        var result = new List<Counter>(count);
        var mega = false;
        foreach (var counter in ranked)
        {
            if (result.Count == count)
            {
                break;
            }

            if (counter.Pokemon.IsMega)
            {
                if (mega)
                {
                    continue;
                }

                mega = true;
            }

            result.Add(counter);
        }

        return result;
    }

    /// <summary>Same type, Mega and type effectiveness of a move against the defender's types.</summary>
    private static double Multiplier(PokemonEntry attacker, string moveType, IReadOnlyList<string> defenderTypes) =>
        RaidFormula.Bonus(attacker, moveType) * TypeChart.Effectiveness(moveType, defenderTypes);

    /// <summary>The boss's same-type bonus and type effectiveness (a Mega boss gets no Mega bonus: that boosts trainers).</summary>
    private static double BossMultiplier(PokemonEntry boss, string moveType, IReadOnlyList<string> defenderTypes) =>
        (boss.Types.Contains(moveType, StringComparer.Ordinal) ? RaidFormula.Stab : 1.0) * TypeChart.Effectiveness(moveType, defenderTypes);

    private IEnumerable<RaidMove> Moves(IEnumerable<string> ids, bool fast) =>
        ids.Where(id => !RaidRanker.ExcludedMoves.Contains(id))
            .Select(id => fast ? _moves.Fast(id) : _moves.Charged(id))
            .OfType<RaidMove>();
}
