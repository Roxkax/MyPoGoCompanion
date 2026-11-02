using MyPoGoCompanion.Engine.Raids;
using MyPoGoCompanion.Engine.Types;

namespace MyPoGoCompanion.Engine.UnitTests;

public sealed class RaidTests
{
    private static readonly RaidRanker _ranker = new(
        [Data.Charmander, Data.Charmeleon, Data.Charizard, Data.CharizardShadow, Data.CharizardMegaY, Data.Kyogre, Data.Sceptile,
         Data.SceptileMega, Data.Zekrom, Data.Magikarp, Data.Entry("aegislash_blade", 681, ["steel"], new(1, 1, 1)),
         Data.Kyogre with { Id = "kyogre_unreleased", Released = false }],
        Data.Moves, Data.Multipliers);

    [Fact]
    public void Stats_Level40_WithTheShadowBonus()
    {
        var cpm = Data.Multipliers.At(40m);
        var normal = RaidFormula.Stats(Data.Charizard, cpm);
        var shadow = RaidFormula.Stats(Data.CharizardShadow, cpm);
        Assert.Equal((238 * cpm, 188 * cpm, (int)Math.Floor(201 * cpm)), (normal.Attack, normal.Defense, normal.Hp));
        Assert.Equal((238 * cpm * 1.2, 188 * cpm * (5.0 / 6)), (shadow.Attack, shadow.Defense));
        Assert.Equal(normal.Hp, shadow.Hp);
    }

    [Fact]
    public void Bonus_SameTypeAndMega()
    {
        Assert.Equal(1.2, RaidFormula.Bonus(Data.Charizard, "fire"));
        Assert.Equal(1.0, RaidFormula.Bonus(Data.Charizard, "dragon"));
        Assert.Equal(1.2 * 1.3, RaidFormula.Bonus(Data.CharizardMegaY, "fire"));
        Assert.Equal(1.1, RaidFormula.Bonus(Data.CharizardMegaY, "water"));
        Assert.Equal(10, RaidFormula.Damage(14, 200, 180, 1.2)); // floor(9.33) + 1
    }

    [Fact]
    public void Score_ExplainsEveryNumber()
    {
        var fire = Data.Moves.Fast("FIRE_SPIN")!;
        var blast = Data.Moves.Charged("BLAST_BURN")!;
        var stats = RaidFormula.Stats(Data.Charizard, Data.Multipliers.At(40m));
        var score = RaidFormula.Neutral(Data.Charizard, stats, fire, blast);
        var d = score.Detail;
        Assert.Equal(900 / stats.Defense, d.IncomingDps);
        Assert.Equal(50, d.ChargedEnergy);
        Assert.Equal(d.FastDamage / 1.1, d.FastDps);
        Assert.Equal(Math.Pow(Math.Pow(score.Dps, 3) * score.Tdo, 0.25), score.Score);
        Assert.Equal(score.Dps * stats.Hp / d.IncomingDps, score.Tdo);
        Assert.Equal(stats.Hp / d.IncomingDps, score.Seconds);
        Assert.Equal(("FIRE_SPIN", "BLAST_BURN"), (score.FastMoveId, score.ChargedMoveId));
    }

    [Fact]
    public void Score_A100EnergyMove_WastesEnergy_AndAWeakChargedMoveIsNotUsed()
    {
        var stats = RaidFormula.Stats(Data.Kyogre, Data.Multipliers.At(40m));
        var pulse = RaidFormula.Neutral(Data.Kyogre, stats, Data.Moves.Fast("WATERFALL")!, Data.Moves.Charged("ORIGIN_PULSE")!);
        Assert.Equal(100 + (0.5 * 8) + (0.5 * pulse.Detail.IncomingDps * 1.2), pulse.Detail.ChargedEnergy);

        // A charged move doing less per second than the fast move is never worth using: fast moves only.
        var karp = RaidFormula.Stats(Data.Magikarp, Data.Multipliers.At(40m));
        var strongFast = RaidMove.FromMilliseconds("STRONG", "water", 40, 500, 0, 10);
        var weakCharged = RaidMove.FromMilliseconds("WEAK", "water", 10, 3000, 1000, -50);
        var weak = RaidFormula.Neutral(Data.Magikarp, karp, strongFast, weakCharged);
        Assert.Equal(weak.Detail.FastDps, weak.Dps);
        Assert.Throws<ArgumentNullException>(() => RaidFormula.Score(karp, null!, Data.Moves.Charged("STRUGGLE")!, 1, 1, 180, 5));
        Assert.Throws<ArgumentNullException>(() => RaidFormula.Score(karp, Data.Moves.Fast("SPLASH")!, null!, 1, 1, 180, 5));
        Assert.Throws<ArgumentNullException>(() => RaidFormula.Stats(null!, 0.79));
        Assert.Throws<ArgumentNullException>(() => RaidFormula.Bonus(null!, "fire"));
    }

    [Fact]
    public void Rank_SameTypeMovesOnly_MegasAside()
    {
        var fire = _ranker.Rank("fire");
        Assert.Equal(["charizard_mega_y", "charizard_shadow", "charizard"], fire.Ranked.Select(r => r.Pokemon.Id));
        Assert.All(fire.Ranked, r => Assert.Equal(("FIRE_SPIN", "BLAST_BURN"), (r.Best.FastMoveId, r.Best.ChargedMoveId)));
        Assert.True(fire.Ranked[1].Top && fire.Ranked[2].Top && !fire.Ranked[0].Top);

        // Fewer than 10 Pokédex numbers: the 10th is 0, so every Mega is a pick.
        Assert.Equal(0, fire.Tenth);
        Assert.True(fire.Ranked[0].MegaPick);
        Assert.True(fire.Ranked[0].EliteCharged);
        Assert.False(fire.Ranked[0].EliteFast);
        Assert.Equal([5, 4], fire.Ranked[2].PreEvolutions);
        Assert.Equal([5, 4], fire.Ranked[0].PreEvolutions);

        Assert.Empty(_ranker.Rank("fairy").Ranked);
        Assert.DoesNotContain(_ranker.Rank("water").Ranked, r => r.Pokemon.Id == "kyogre_unreleased");
        Assert.Equal(18, _ranker.RankAll().Count);
        Assert.Contains("RETURN (fast)", Data.Moves.Missing.Concat(["RETURN (fast)"]));
    }

    [Fact]
    public void Rank_PicksTenPokedexNumbers_AndMegasAboveTheTenth()
    {
        var many = Enumerable.Range(1, 12)
            .Select(i => Data.Entry($"fire{i}", 1000 + i, ["fire"], new(100 + (10 * i), 150, 150), fast: ["EMBER"], charged: ["FLAMETHROWER"]))
            .Append(Data.Entry("fire_mega_low", 1001, ["fire"], new(60, 150, 150), fast: ["EMBER"], charged: ["FLAMETHROWER"], mega: true))
            .Append(Data.Entry("fire_mega_high", 1012, ["fire"], new(400, 150, 150), fast: ["EMBER"], charged: ["FLAMETHROWER"], mega: true))
            .ToList();
        var ranking = new RaidRanker(many, Data.Moves, Data.Multipliers).Rank("fire");
        Assert.Equal(10, ranking.Ranked.Count(r => r.Top));
        Assert.Equal(ranking.Ranked.Single(r => r.Pokemon.Id == "fire3").Best.Score, ranking.Tenth);
        Assert.True(ranking.Ranked.Single(r => r.Pokemon.Id == "fire_mega_high").MegaPick);
        Assert.False(ranking.Ranked.Single(r => r.Pokemon.Id == "fire_mega_low").MegaPick);

        var (attackers, pre) = RaidRanker.Selection([ranking, _ranker.Rank("fire")]);
        Assert.Equal([6, .. Enumerable.Range(1003, 10)], attackers); // the top 10 here (fire3–fire12) and Charizard
        Assert.Equal([4, 5], pre);
        Assert.Throws<ArgumentNullException>(() => RaidRanker.Selection(null!));
    }

    [Fact]
    public void PreEvolutions_OfAMega_FromItsBaseForm()
    {
        Assert.Equal([5, 4], _ranker.PreEvolutions(Data.CharizardMegaY));
        Assert.Empty(_ranker.PreEvolutions(Data.Charmander));

        // A Mega whose base id isn't listed: the same Pokédex number's regular form.
        var odd = Data.CharizardMegaY with { Id = "charizard_megaz" };
        Assert.Equal([5, 4], _ranker.PreEvolutions(odd));
        var alone = new RaidRanker([Data.SceptileMega], Data.Moves, Data.Multipliers);
        Assert.Empty(alone.PreEvolutions(Data.SceptileMega));
        Assert.Throws<ArgumentNullException>(() => _ranker.PreEvolutions(null!));
    }

    [Fact]
    public void Movesets_EveryCombination_BestFirst()
    {
        var movesets = _ranker.Movesets(Data.Charizard);
        Assert.Equal(3 * 4, movesets.Count);
        Assert.Contains(movesets, m => m.ChargedMoveId == "RETURN");
        Assert.True(movesets.Zip(movesets.Skip(1)).All(pair => pair.First.Score >= pair.Second.Score));
        Assert.Throws<ArgumentNullException>(() => _ranker.Movesets(null!));
        Assert.Throws<ArgumentNullException>(() => new RaidRanker(null!, Data.Moves, Data.Multipliers));
        Assert.Throws<ArgumentNullException>(() => new RaidRanker([], null!, Data.Multipliers));
        Assert.Throws<ArgumentNullException>(() => new RaidRanker([], Data.Moves, null!));
    }

    [Fact]
    public void MoveBook_RemembersWhatItLacks()
    {
        var book = new RaidMoveBook([("EMBER", true, RaidMove.FromMilliseconds("EMBER", "fire", 10, 1000, 0, 10))]);
        Assert.NotNull(book.Fast("EMBER"));
        Assert.Null(book.Charged("EMBER"));
        Assert.Null(book.Fast("HIDDEN_POWER_BUG"));
        Assert.Equal(["EMBER", "HIDDEN_POWER_BUG (fast)"], book.Missing);
        Assert.Equal(1.0, book.Fast("EMBER")!.Duration);
        Assert.Throws<ArgumentNullException>(() => new RaidMoveBook(null!));
    }

    [Fact]
    public void TypeChart_GoMultipliers()
    {
        Assert.Equal(18, TypeChart.Types.Count);
        var pairs = TypeChart.Types.SelectMany(a => TypeChart.Types.Select(d => TypeChart.Against(a, d))).ToList();
        Assert.Equal(51, pairs.Count(m => m == TypeChart.SuperEffective));
        Assert.Equal(61, pairs.Count(m => m == TypeChart.NotVeryEffective));
        Assert.Equal(8, pairs.Count(m => m == TypeChart.DoubleResisted));

        Assert.Equal(1.6 * 1.6, TypeChart.Effectiveness("ice", ["dragon", "flying"]));
        Assert.Equal(0.390625 * 1.6, TypeChart.Effectiveness("ground", ["flying", "electric"]));
        Assert.Equal(0.625 * 0.625, TypeChart.Effectiveness("fire", ["water", "dragon"]));
        Assert.Equal(1.6, TypeChart.Effectiveness("water", ["fire", "fire"]));
        Assert.Equal(1.0, TypeChart.Effectiveness("normal", ["water"]));
        var defending = TypeChart.Defending(["fire", "flying"]);
        Assert.Equal(2.56, defending.Single(d => d.Type == "rock").Multiplier, 12);
        Assert.Equal(0.390625, defending.Single(d => d.Type == "grass").Multiplier, 12);
        Assert.Throws<ArgumentException>(() => TypeChart.Against("sound", "fire"));
        Assert.Throws<ArgumentException>(() => TypeChart.Against("fire", "sound"));
        Assert.Throws<ArgumentNullException>(() => TypeChart.Against(null!, "fire"));
        Assert.Throws<ArgumentNullException>(() => TypeChart.Effectiveness("fire", null!));
    }
}
