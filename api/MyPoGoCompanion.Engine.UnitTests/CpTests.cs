namespace MyPoGoCompanion.Engine.UnitTests;

public sealed class CpTests
{
    private static readonly BaseStats _charizard = Data.Charizard.Stats;

    [Fact]
    public void Max_IsTheHundoAtLevels40_50And51()
    {
        Assert.Equal([new(40m, 2889), new(50m, 3266), new(51m, 3305)], Cp.Max(_charizard, Data.Multipliers));
    }

    [Fact]
    public void Perfect_DefaultsToTheCatchLevels()
    {
        Assert.Equal([1238, 1651, 2064, 2476, 2683], Cp.Perfect(_charizard, Data.Multipliers).Select(level => level.Cp));
        Assert.Equal([15m, 20m, 25m, 30m, 35m], Cp.Perfect(_charizard, Data.Multipliers).Select(level => level.Level));
    }

    [Fact]
    public void Of_IsAtLeastTen()
    {
        Assert.Equal(10, Cp.Of(new BaseStats(1, 1, 1), new Ivs(0, 0, 0), 1m, Data.Multipliers));
        Assert.Equal(2889, Cp.Of(_charizard, Ivs.Perfect, Data.Multipliers.At(40m)));
    }

    [Fact]
    public void Multipliers_ByLevel_InHalfLevels()
    {
        Assert.Equal(101, Data.Multipliers.Count);
        Assert.Equal(51m, Data.Multipliers.MaxLevel);
        Assert.Equal(0.790300011634826, Data.Multipliers.At(40m));
        Assert.Equal(78, Data.Multipliers.IndexOf(40m));
        Assert.Equal(40.5m, CpMultipliers.LevelAt(79));
        Assert.Throws<ArgumentOutOfRangeException>(() => Data.Multipliers.At(40.25m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Data.Multipliers.At(0.5m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Data.Multipliers.At(52m));
        Assert.Throws<ArgumentException>(() => new CpMultipliers([]));
        Assert.Throws<ArgumentException>(() => new CpMultipliers([0.1, 0]));
        Assert.Throws<ArgumentNullException>(() => new CpMultipliers(null!));
        Assert.Throws<ArgumentNullException>(() => Cp.Of(_charizard, Ivs.Perfect, 40m, null!));
        Assert.Throws<ArgumentNullException>(() => Cp.Perfect(_charizard, null!));
    }

    [Fact]
    public void Ivs_IndexOrder_AttackThenDefenseThenHp()
    {
        Assert.Equal(4096, Ivs.All.Count);
        Assert.Equal(new Ivs(0, 0, 1), Ivs.All[1]);
        Assert.Equal(new Ivs(0, 1, 0), Ivs.All[16]);
        Assert.Equal(new Ivs(1, 0, 0), Ivs.All[256]);
        Assert.Equal(4095, Ivs.Perfect.Index);
        Assert.Equal("15/15/15", Ivs.Perfect.ToString());
        Assert.True(new Ivs(0, 15, 7).IsValid);
        Assert.False(new Ivs(16, 0, 0).IsValid);
        Assert.False(new Ivs(0, -1, 0).IsValid);
        Assert.False(new Ivs(0, 0, 16).IsValid);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(14, 3)]
    [InlineData(15, 4)]
    public void Appraisal_Bars(int iv, int bar) => Assert.Equal(bar, Appraisal.Bar(iv));

    [Fact]
    public void Appraisal_CellsAndBadIvs()
    {
        Assert.Equal(124, Appraisal.Cell(Ivs.Perfect));
        Assert.Equal((3 * 25) + (4 * 5) + 2, Appraisal.Cell(new Ivs(12, 15, 7)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Appraisal.Bar(16));
        Assert.Throws<ArgumentOutOfRangeException>(() => Appraisal.Bar(-1));
    }

    [Fact]
    public void Entry_MegaAndShadowRules_AsInPython()
    {
        Assert.True(PokemonEntry.IsMegaId("charizard_mega_y", []));
        Assert.True(PokemonEntry.IsMegaId("kyogre_primal", []));
        Assert.True(PokemonEntry.IsMegaId("x", ["mega"]));
        Assert.False(PokemonEntry.IsMegaId("meganium", []));
        Assert.True(PokemonEntry.IsShadowId("charizard_shadow", []));
        Assert.True(PokemonEntry.IsShadowId("x", ["shadow"]));
        Assert.False(PokemonEntry.IsShadowId("charizard", ["mega"]));
        Assert.Equal("charizard", Data.CharizardShadow.FormId);
        Assert.Equal("charizard", Data.Charizard.FormId);
        Assert.Contains("aegislash_blade", PokemonEntry.BattleOnly);
        Assert.Throws<ArgumentNullException>(() => PokemonEntry.IsMegaId(null!, []));
        Assert.Throws<ArgumentNullException>(() => PokemonEntry.IsMegaId("x", null!));
        Assert.Throws<ArgumentNullException>(() => PokemonEntry.IsShadowId(null!, []));
        Assert.Throws<ArgumentNullException>(() => PokemonEntry.IsShadowId("x", null!));
    }
}
