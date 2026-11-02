using MyPoGoCompanion.Engine.Pvp;

namespace MyPoGoCompanion.Engine.UnitTests;

public sealed class PvpTests
{
    private static readonly LeagueStats _charizardGreat = LeagueStats.Calculate(Data.Charizard.Stats, Leagues.Great, Data.Multipliers);

    [Fact]
    public void GreatLeague_Charizard_RankOneAsThePythonBuild()
    {
        // tests/golden/keeper-filters/outputs/pvp_boxes.json: 0/15/13, level 19.5, CP 1500.
        var best = _charizardGreat.Best;
        Assert.Equal((new Ivs(0, 15, 13), 19.5m, 1500), (best.Ivs, best.Level, best.Cp));
        Assert.Equal(1, _charizardGreat.RankOf(best.Ivs));
        Assert.Equal(1.0, _charizardGreat.ShareOfBest(best.Ivs));
        Assert.Equal(best, _charizardGreat.Ranking().First());
        Assert.Equal(Ivs.Count, _charizardGreat.Ranking().Count());
        Assert.Equal(Leagues.Great, _charizardGreat.Cap);
        Assert.Equal(Data.Charizard.Stats, _charizardGreat.Stats);
    }

    [Fact]
    public void EverySpread_StaysUnderTheCap_AtItsHighestLevel()
    {
        foreach (var ivs in Ivs.All)
        {
            var spread = _charizardGreat[ivs];
            Assert.True(spread.Cp <= Leagues.Great);
            if (spread.Level < Leagues.MaxLevel)
            {
                Assert.True(Cp.Of(Data.Charizard.Stats, ivs, spread.Level + 0.5m, Data.Multipliers) > Leagues.Great);
            }
        }

        var hundo = _charizardGreat[Ivs.Perfect];
        Assert.True(_charizardGreat.RankOf(Ivs.Perfect) > 1);
        Assert.True(_charizardGreat.ShareOfBest(Ivs.Perfect) < 1);
        Assert.Equal(hundo.StatProduct, _charizardGreat.StatProduct(Ivs.Perfect.Index));
    }

    [Fact]
    public void MasterLeague_TopIsTheHundo_ThoughHpRoundingTiesIt()
    {
        var master = LeagueStats.Calculate(Data.Charizard.Stats, Leagues.Master, Data.Multipliers);

        // HP rounds down, so 15/15/14 has the same stat product: the keeper filters take the first (Python), the
        // screens the higher CP.
        Assert.Equal(new Ivs(15, 15, 14), master.Best.Ivs);
        Assert.Equal((Ivs.Perfect, 50m, 3266), (master.Top.Ivs, master.Top.Level, master.Top.Cp));
        Assert.Equal(1, master.RankOf(new Ivs(15, 15, 14)));
        Assert.Equal(master.Top, master.Ranking().First());
    }

    [Fact]
    public void ASpreadOverTheCapAtLevelOne_StaysAtLevelOne()
    {
        var huge = LeagueStats.Calculate(new BaseStats(5000, 5000, 5000), Leagues.Great, Data.Multipliers);
        Assert.All(Ivs.All, ivs => Assert.Equal(1m, huge[ivs].Level));
        Assert.Throws<ArgumentNullException>(() => LeagueStats.Calculate(Data.Charizard.Stats, 1500, null!));
    }

    [Fact]
    public void Keepers_AreTheSpreadsWithin98PercentOfRankOne()
    {
        var keepers = _charizardGreat.Keepers(KeeperBoxes.KeepShare);
        Assert.True(keepers[_charizardGreat.BestIndex]);
        Assert.Equal(Ivs.All.Count(ivs => _charizardGreat.ShareOfBest(ivs) >= 0.98), keepers.Count(k => k));
    }

    [Fact]
    public void BestBox_ExactlyTheKeepers_HasF1One()
    {
        // Keepers: Attack bar 0–1, Defense and HP bar 4 (15).
        var keepers = Ivs.All.Select(ivs => ivs.Attack <= 5 && ivs.Defense == 15 && ivs.Stamina == 15).ToArray();
        var fit = KeeperBoxes.BestBox(keepers, [Appraisal.Cell(new Ivs(0, 15, 15))]);
        Assert.Equal(new KeeperBox(new BarRange(0, 1), new BarRange(4, 4), new BarRange(4, 4)), fit.Box);
        Assert.Equal((1.0, 6, 6, 6), (fit.F1, fit.TruePositives, fit.InBox, fit.Keepers));
        Assert.True(fit.Box.Contains(new Ivs(3, 15, 15)));
        Assert.False(fit.Box.Contains(new Ivs(6, 15, 15)));
    }

    [Fact]
    public void BestBox_MustHoldTheRequiredCells()
    {
        var keepers = Ivs.All.Select(ivs => ivs == new Ivs(0, 15, 15)).ToArray();
        var fit = KeeperBoxes.BestBox(keepers, [Appraisal.Cell(new Ivs(0, 15, 15)), Appraisal.Cell(Ivs.Perfect)]);
        Assert.Equal(new KeeperBox(new BarRange(0, 4), new BarRange(4, 4), new BarRange(4, 4)), fit.Box);
        Assert.Equal(3375, KeeperBoxes.All.Count);
        Assert.Equal(15, KeeperBoxes.Ranges.Count);
        Assert.Equal(BarRange.Any, KeeperBoxes.Ranges[4]);
        Assert.Equal(Ivs.Count, KeeperBoxes.CellSizes.Sum());
    }

    [Fact]
    public void BestBox_NoBoxHoldsTheCells_FallsBackToTheTightest()
    {
        var fit = KeeperBoxes.BestBox(new bool[Ivs.Count], [999]);
        Assert.Equal(-1.0, fit.F1);
        Assert.Equal(1, fit.InBox);
        Assert.Throws<ArgumentException>(() => KeeperBoxes.BestBox(new bool[10], []));
        Assert.Throws<ArgumentNullException>(() => KeeperBoxes.BestBox(null!, []));
        Assert.Throws<ArgumentNullException>(() => KeeperBoxes.BestBox(new bool[Ivs.Count], null!));
    }

    [Fact]
    public void Calculator_JudgesByTheFinalEvolution()
    {
        var calculator = new KeeperCalculator([Data.Charmander, Data.Charmeleon, Data.Charizard, Data.CharizardShadow, Data.CharizardMegaY], Data.Multipliers);
        Assert.Equal(["charmander", "charmeleon", "charizard"], calculator.Order);

        var boxes = calculator.Boxes(Leagues.Great, [], dexes: [6, 4, 5]);
        Assert.Equal([4, 5, 6], boxes.Select(box => box.Dex));
        Assert.All(boxes, box => Assert.Equal(["charizard"], box.JudgedBy));
        Assert.Equal(["charmander"], boxes[0].Forms);
        var rank1 = Assert.Single(boxes[0].RankOnes);
        Assert.Equal(("charizard", new Ivs(0, 15, 13)), (rank1.FormId, rank1.Ivs));
        Assert.True(boxes[0].Fit.Box.Contains(rank1.Ivs));

        // A middle stage in the league's top 150 judges itself too (Meta); Final ignores that.
        var meta = calculator.Boxes(Leagues.Great, ["charmeleon"], dexes: [5]);
        Assert.Equal(["charizard", "charmeleon"], meta[0].JudgedBy);
        Assert.Equal(["charizard"], calculator.Boxes(Leagues.Great, ["charmeleon"], KeeperPolicy.Final, [5])[0].JudgedBy);
        Assert.Equal(["charizard", "charmander"], calculator.Boxes(Leagues.Great, [], KeeperPolicy.All, [4])[0].JudgedBy);
        Assert.Equal(["charmander"], calculator.Boxes(Leagues.Great, [], KeeperPolicy.Cap, [4])[0].JudgedBy);

        Assert.Equal(3, calculator.RankOnes(Leagues.Master).Count());
        Assert.All(calculator.RankOnes(Leagues.Master), one => Assert.Equal(Ivs.Perfect, one.Ivs));
    }

    [Fact]
    public void Calculator_EveryNumberNeedsAForm_AndLoopsDontHang()
    {
        var calculator = new KeeperCalculator([Data.Charizard], Data.Multipliers);
        var missing = Assert.Throws<InvalidOperationException>(() => calculator.Boxes(Leagues.Great, []));
        Assert.Equal("No form found for Pokédex #1.", missing.Message);

        // Two forms that "evolve" into each other (bad data) still finish.
        var a = Data.Entry("a", 1, ["normal"], new(100, 100, 100), parent: "b", evolutions: ["b"]);
        var b = Data.Entry("b", 2, ["normal"], new(150, 150, 150), parent: "a", evolutions: ["a"]);
        var loop = new KeeperCalculator([a, b, a with { Name = "again" }], Data.Multipliers);
        Assert.Equal(["a", "b"], loop.Order);
        Assert.Equal("again", loop.Species["a"].Name);
        Assert.Equal(2, loop.Boxes(Leagues.Great, [], dexes: [1, 2]).Count);

        var withOthers = new KeeperCalculator(
            [Data.Entry("aegislash_blade", 681, ["steel"], new(1, 1, 1)), Data.Entry("future", 1026, ["normal"], new(1, 1, 1))], Data.Multipliers);
        Assert.Empty(withOthers.Order);
        Assert.Throws<ArgumentNullException>(() => new KeeperCalculator(null!, Data.Multipliers));
        Assert.Throws<ArgumentNullException>(() => new KeeperCalculator([], null!));
        Assert.Throws<ArgumentNullException>(() => calculator.Boxes(Leagues.Great, null!));
    }
}
