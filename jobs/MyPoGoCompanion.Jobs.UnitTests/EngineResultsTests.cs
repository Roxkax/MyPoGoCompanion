using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>The engine's results in the daily import (E02-F03), from the fixtures' 28 species.</summary>
public sealed class EngineResultsTests
{
    private static readonly ImportedGameData _data = Fixtures.Built;

    [Fact]
    public void BestIvs_EveryNonMegaForm_InAllThreeLeagues()
    {
        var charizard = _data.PvpBestIvs.Where(best => best.FormId == "charizard").ToDictionary(best => best.League);
        Assert.Equal((0, 15, 13, 19.5m, 1500), (charizard[League.Great].AttackIv, charizard[League.Great].DefenseIv,
            charizard[League.Great].StaminaIv, charizard[League.Great].Level, charizard[League.Great].Cp));
        Assert.Equal((15, 15, 15, 50m), (charizard[League.Master].AttackIv, charizard[League.Master].DefenseIv,
            charizard[League.Master].StaminaIv, charizard[League.Master].Level));
        Assert.True(charizard[League.Great].StatProduct > 0);

        var forms = _data.Forms.Where(form => form.Kind is not (FormKind.Mega or FormKind.Primal)).Select(form => form.Id).Order().ToList();
        foreach (var league in new[] { League.Great, League.Ultra, League.Master })
        {
            Assert.Equal(forms, _data.PvpBestIvs.Where(best => best.League == league).Select(best => best.FormId).Order());
        }
    }

    [Fact]
    public void KeeperBoxes_EveryPokedexNumber_GreatAndUltra()
    {
        Assert.Equal(_data.Species.Count * 2, _data.PvpKeeperBoxes.Count);
        Assert.DoesNotContain(_data.PvpKeeperBoxes, box => box.League == League.Master);
        var charmander = _data.PvpKeeperBoxes.Single(box => box.League == League.Great && box.Dex == 4);
        Assert.Equal(["charizard"], charmander.JudgedBy);
        Assert.Equal((0, 1, 3, 4, 3, 4), (charmander.AttackBarMin, charmander.AttackBarMax, charmander.DefenseBarMin,
            charmander.DefenseBarMax, charmander.StaminaBarMin, charmander.StaminaBarMax));
    }

    [Fact]
    public void RaidRankings_RankedPerType_ShadowsFlagged()
    {
        foreach (var type in _data.RaidRankings.GroupBy(ranking => ranking.AttackingTypeId))
        {
            Assert.Equal(Enumerable.Range(1, type.Count()), type.Select(ranking => ranking.Rank));
            Assert.True(type.Zip(type.Skip(1)).All(pair => pair.First.Score >= pair.Second.Score));
        }

        var fire = _data.RaidRankings.Where(ranking => ranking.AttackingTypeId == "fire").ToList();
        Assert.Contains(fire, ranking => ranking.FormId == "charizard" && ranking.IsShadow);
        Assert.Contains(fire, ranking => ranking.FormId == "charizard_mega_y" && !ranking.IsShadow);
        var formIds = _data.Forms.Select(form => form.Id).ToHashSet();
        Assert.All(_data.RaidRankings, ranking => Assert.Contains(ranking.FormId, formIds));
    }

    [Fact]
    public void RaidMovesets_EveryCombination_BestFirst()
    {
        var charizard = _data.RaidMovesets.Where(m => m.FormId == "charizard" && !m.IsShadow).ToList();
        Assert.Equal(Enumerable.Range(1, charizard.Count), charizard.Select(m => m.Rank));
        Assert.True(charizard.Zip(charizard.Skip(1)).All(pair => pair.First.Score >= pair.Second.Score));
        Assert.Contains(_data.RaidMovesets, m => m.FormId == "charizard" && m.IsShadow);
        Assert.Equal(_data.RaidMovesets.Count, _data.RaidMovesets.Select(m => (m.FormId, m.IsShadow, m.FastMoveId, m.ChargedMoveId)).Distinct().Count());
        var moveIds = _data.Moves.Select(move => move.Id).ToHashSet();
        Assert.All(_data.RaidMovesets, m => Assert.True(moveIds.Contains(m.FastMoveId) && moveIds.Contains(m.ChargedMoveId)));
    }

    [Fact]
    public void Inputs_EveryEntry_WithTheGameMastersRaidNumbers()
    {
        var files = Fixtures.Sources();
        var pvpoke = PvpokeSource.ReadGameMaster(files.PvpokeGameMaster);
        var inputs = EngineInputs.From(pvpoke, GameMasterSource.Read(files.GameMaster),
            PvpokeSource.ReadCpMultipliers(files.CpMultipliers, GameDataBuilder.MaxLevel));
        Assert.Equal(pvpoke.Pokemon.Count, inputs.Entries.Count);
        var shadow = inputs.Entries.Single(entry => entry.Id == "charizard_shadow");
        Assert.True(shadow.IsShadow);
        Assert.Equal("charmeleon_shadow", shadow.Parent);
        Assert.True(inputs.Entries.Single(entry => entry.Id == "charizard_mega_y").IsMega);
        var fireSpin = inputs.Moves.Fast("FIRE_SPIN")!;
        Assert.Equal("fire", fireSpin.Type);
        Assert.True(fireSpin.Duration > 0 && fireSpin.Energy > 0);
        Assert.Null(inputs.Moves.Charged("FIRE_SPIN"));
        Assert.Equal(51m, inputs.Multipliers.MaxLevel);
        Assert.Throws<ArgumentNullException>(() => EngineInputs.From(null!, GameMasterSource.Read(files.GameMaster), []));
    }

    [Fact]
    public void HasEveryLevel_NeedsEveryHalfLevelToFifty()
    {
        var full = Enumerable.Range(0, 99).Select(i => (CpMultipliers.LevelAt(i), 0.1 + (i / 200.0))).ToList();
        Assert.True(EngineInputs.HasEveryLevel(full));
        Assert.False(EngineInputs.HasEveryLevel(full.Take(98).ToList()));
        Assert.False(EngineInputs.HasEveryLevel([(1m, 0.094), (50m, 0.8403)]));
        Assert.False(EngineInputs.HasEveryLevel([.. full.Skip(1), (50.5m, 0.84)]));
        Assert.Throws<ArgumentNullException>(() => EngineInputs.HasEveryLevel(null!));
    }

    [Fact]
    public void APokedexNumberWithoutAForm_FailsTheImportInPlainWords()
    {
        var files = Fixtures.Sources();
        var inputs = EngineInputs.From(PvpokeSource.ReadGameMaster(files.PvpokeGameMaster), GameMasterSource.Read(files.GameMaster),
            PvpokeSource.ReadCpMultipliers(files.CpMultipliers, GameDataBuilder.MaxLevel));
        var data = new ImportedGameData();
        data.Species.Add(new Species { Dex = 7, NameEn = "Squirtle", FamilyId = "FAMILY_SQUIRTLE" });
        var failure = Assert.Throws<ImportException>(() => EngineResults.Add(data, inputs, []));
        Assert.Equal("The Great League keeper boxes can't be calculated: No form found for Pokédex #7.", failure.Message);
        Assert.Throws<ArgumentNullException>(() => EngineResults.Add(null!, inputs, []));
    }
}
