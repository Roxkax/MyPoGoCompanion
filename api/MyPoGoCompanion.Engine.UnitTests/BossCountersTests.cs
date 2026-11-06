using MyPoGoCompanion.Engine.Raids;
using MyPoGoCompanion.Engine.Types;

namespace MyPoGoCompanion.Engine.UnitTests;

public sealed class BossCountersTests
{
    private static readonly RaidBossSetup _kyogre = new(Data.Kyogre, BossTier.FiveStar,
        [Data.Moves.Fast("WATERFALL")!],
        [Data.Moves.Charged("ORIGIN_PULSE")!, Data.Moves.Charged("SURF")!, Data.Moves.Charged("BLIZZARD")!, Data.Moves.Charged("THUNDER")!]);

    private static readonly BossCounters _counters = new(
        [Data.Charizard, Data.CharizardShadow, Data.CharizardMegaY, Data.Sceptile, Data.SceptileMega, Data.Zekrom, Data.Magikarp, Data.Ditto,
         Data.Zekrom with { Id = "zekrom_unreleased", Released = false }, Data.Entry("aegislash_blade", 681, ["steel"], new(1, 1, 1))],
        Data.Moves, Data.Multipliers);

    [Theory]
    [InlineData(BossTier.OneStar, 0.5974)]
    [InlineData(BossTier.ThreeStar, 0.73)]
    [InlineData(BossTier.FiveStar, 0.79)]
    [InlineData(BossTier.Mega, 0.79)]
    [InlineData(BossTier.LegendaryMega, 0.79)]
    [InlineData(BossTier.Elite, 0.79)]
    public void TierMultiplier(BossTier tier, double cpm) => Assert.Equal(cpm, BossCounters.TierMultiplier(tier));

    [Fact]
    public void BossStats_FromTheTierWith15Ivs()
    {
        var stats = BossCounters.BossStats(_kyogre);
        Assert.Equal((285 * 0.79, 243 * 0.79), (stats.Attack, stats.Defense));
        Assert.Throws<ArgumentOutOfRangeException>(() => BossCounters.TierMultiplier((BossTier)99));
        Assert.Throws<ArgumentNullException>(() => BossCounters.BossStats(null!));
    }

    [Fact]
    public void BossDps_FastMovesUntilTheChargedMove_TwoSecondsApart()
    {
        var defender = new BattleStats(200, 150, 150);
        var waterfall = Data.Moves.Fast("WATERFALL")!;
        var surf = Data.Moves.Charged("SURF")!;
        var attack = BossCounters.BossStats(_kyogre).Attack;
        var fast = RaidFormula.Damage(16, attack, 150, 1.2);
        var charged = RaidFormula.Damage(65, attack, 150, 1.2);

        // Surf costs 50, Waterfall gains 8: 7 fast moves per Surf.
        var expected = ((7.0 * fast) + charged) / ((7 * (1.2 + 2)) + 1.7 + 2);
        Assert.Equal(expected, BossCounters.BossDps(_kyogre, waterfall, surf, defender, ["normal"]), 12);

        // Water against Fire is super effective: the damage taken goes up.
        Assert.True(BossCounters.BossDps(_kyogre, waterfall, surf, defender, ["fire"]) > expected);

        // A fast move that gains no energy: fast moves only.
        var noEnergy = waterfall with { Energy = 0 };
        Assert.Equal(fast / 3.2, BossCounters.BossDps(_kyogre, noEnergy, surf, defender, ["normal"]), 12);
        Assert.Throws<ArgumentNullException>(() => BossCounters.BossDps(null!, waterfall, surf, defender, []));
        Assert.Throws<ArgumentNullException>(() => BossCounters.BossDps(_kyogre, null!, surf, defender, []));
        Assert.Throws<ArgumentNullException>(() => BossCounters.BossDps(_kyogre, waterfall, null!, defender, []));
    }

    [Fact]
    public void BossDps_AMegaBossHasNoMegaBonus()
    {
        var mega = _kyogre with { Boss = Data.Kyogre with { IsMega = true } };
        var defender = new BattleStats(200, 150, 150);
        Assert.Equal(
            BossCounters.BossDps(_kyogre, _kyogre.FastMoves[0], _kyogre.ChargedMoves[1], defender, ["normal"]),
            BossCounters.BossDps(mega, _kyogre.FastMoves[0], _kyogre.ChargedMoves[1], defender, ["normal"]));
    }

    [Fact]
    public void Rank_GrassAndElectricBeatKyogre_FireDoesNot()
    {
        var ranked = _counters.Rank(_kyogre);
        Assert.Equal("sceptile_mega", ranked[0].Pokemon.Id);
        Assert.Equal(1.0, ranked[0].ShareOfBest);
        Assert.Equal(("BULLET_SEED", "FRENZY_PLANT"), (ranked[0].FastMoveId, ranked[0].ChargedMoveId));
        Assert.True(ranked.Zip(ranked.Skip(1)).All(pair => pair.First.Score >= pair.Second.Score));
        var ids = ranked.Select(c => c.Pokemon.Id).ToList();
        Assert.True(ids.IndexOf("zekrom") < ids.IndexOf("charizard"));
        Assert.DoesNotContain(ranked, c => c.Pokemon.Id is "zekrom_unreleased" or "aegislash_blade");
        Assert.DoesNotContain(ranked, c => c.Pokemon.Id == "ditto"); // Transform can't be scored
        Assert.All(ranked, c => Assert.True(double.IsFinite(c.Score)));

        // Every one of the boss's 4 movesets counts, equally.
        var sceptile = ranked.Single(c => c.Pokemon.Id == "sceptile");
        Assert.Equal(4, sceptile.PerBossMoveset.Count);
        Assert.Equal(sceptile.PerBossMoveset.Average(m => m.Score.Score), sceptile.Score, 12);
        Assert.Equal(sceptile.PerBossMoveset.Average(m => m.Score.Seconds), sceptile.Seconds, 12);
        Assert.True(sceptile.EliteCharged);
        Assert.False(sceptile.EliteFast);
        Assert.InRange(sceptile.ShareOfBest, 0, 1);
        Assert.True(sceptile.Dps > 0 && sceptile.Tdo > 0);

        // Return and Frustration never count.
        Assert.DoesNotContain(ranked, c => c.ChargedMoveId is "RETURN" or "FRUSTRATION");
    }

    [Fact]
    public void Rank_PickingTheBossMoves_ReRanks()
    {
        var thunder = _counters.Rank(_kyogre, "WATERFALL", "THUNDER");
        var blizzard = _counters.Rank(_kyogre, bossCharged: "BLIZZARD");
        Assert.All(thunder, c => Assert.Equal(("WATERFALL", "THUNDER"), (Assert.Single(c.PerBossMoveset).BossFastMoveId, c.PerBossMoveset[0].BossChargedMoveId)));

        // Blizzard hits the Grass and Dragon counters hard, Thunder doesn't hit Zekrom much.
        var sceptileThunder = thunder.Single(c => c.Pokemon.Id == "sceptile").Seconds;
        var sceptileBlizzard = blizzard.Single(c => c.Pokemon.Id == "sceptile").Seconds;
        Assert.True(sceptileBlizzard < sceptileThunder);

        Assert.Throws<ArgumentException>(() => _counters.Rank(_kyogre, "SPLASH"));
        Assert.Throws<ArgumentNullException>(() => _counters.Rank(null!));
        Assert.Empty(new BossCounters([], Data.Moves, Data.Multipliers).Rank(_kyogre));
        Assert.Throws<ArgumentNullException>(() => new BossCounters(null!, Data.Moves, Data.Multipliers));
        Assert.Throws<ArgumentNullException>(() => new BossCounters([], null!, Data.Multipliers));
        Assert.Throws<ArgumentNullException>(() => new BossCounters([], Data.Moves, null!));
    }

    [Fact]
    public void WithOneMega_KeepsOnlyTheBestMega()
    {
        var ranked = _counters.Rank(_kyogre);
        var shown = BossCounters.WithOneMega(ranked, 4);
        Assert.Equal(4, shown.Count);
        Assert.Single(shown, c => c.Pokemon.IsMega);
        Assert.Equal("sceptile_mega", shown[0].Pokemon.Id);
        Assert.DoesNotContain(shown, c => c.Pokemon.Id == "charizard_mega_y");
        Assert.Equal(ranked.Count - 1, BossCounters.WithOneMega(ranked, 100).Count);
        Assert.Throws<ArgumentNullException>(() => BossCounters.WithOneMega(null!));
        Assert.True(TypeChart.Effectiveness("grass", Data.Kyogre.Types) > 1);
    }
}
