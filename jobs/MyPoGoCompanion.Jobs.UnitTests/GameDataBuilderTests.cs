using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>The game data built from real (trimmed) source files.</summary>
public sealed class GameDataBuilderTests
{
    private static readonly ImportedGameData _data = Fixtures.Built;

    private static Form Form(string id) => Assert.Single(_data.Forms, form => form.Id == id);

    private static Move Move(string id) => Assert.Single(_data.Moves, move => move.Id == id);

    private static Evolution Evolution(string from, string to) =>
        Assert.Single(_data.Evolutions, evolution => evolution.FromFormId == from && evolution.ToFormId == to);

    [Fact]
    public void Types_AreTheEighteenInTheGamesOrder()
    {
        Assert.Equal(18, _data.Types.Count);
        Assert.Equal(("normal", 1), (_data.Types[0].Id, _data.Types[0].SortOrder));
        Assert.Equal(("fairy", "Fairy", "Fada", 18), (_data.Types[17].Id, _data.Types[17].NameEn, _data.Types[17].NamePt, _data.Types[17].SortOrder));
    }

    [Fact]
    public void Species_OneLinePerPokedexNumber_WithGenerationCategoryAndFamily()
    {
        Assert.Equal(_data.Species.Count, _data.Species.Select(s => s.Dex).Distinct().Count());
        var bulbasaur = Assert.Single(_data.Species, s => s.Dex == 1);
        Assert.Equal(("Bulbasaur", 1, SpeciesCategory.None, "FAMILY_BULBASAUR"), (bulbasaur.NameEn, bulbasaur.Generation, bulbasaur.Category, bulbasaur.FamilyId));
        var zacian = Assert.Single(_data.Species, s => s.Dex == 888);
        Assert.Equal(("Zacian", 8, SpeciesCategory.Legendary), (zacian.NameEn, zacian.Generation, zacian.Category));
        Assert.Equal(9, Assert.Single(_data.Species, s => s.Dex == 1000).Generation);
        Assert.Contains("zard", Assert.Single(_data.Species, s => s.Dex == 6).Nicknames);
        Assert.All(_data.Species, s => Assert.Contains(_data.Families, f => f.Id == s.FamilyId));
    }

    [Fact]
    public void Forms_KindsMainFormsAndTheGameMasterMatch()
    {
        Assert.Equal((FormKind.Base, true, "BULBASAUR"), (Form("bulbasaur").Kind, Form("bulbasaur").IsDefault, Form("bulbasaur").GameMasterId));
        Assert.Equal((FormKind.Regional, false, "VULPIX_ALOLA"), (Form("vulpix_alolan").Kind, Form("vulpix_alolan").IsDefault, Form("vulpix_alolan").GameMasterId));
        Assert.Equal((FormKind.Mega, "venusaur", null), (Form("venusaur_mega").Kind, Form("venusaur_mega").BaseFormId, Form("venusaur_mega").GameMasterId));
        Assert.Equal(FormKind.Mega, Form("charizard_mega_x").Kind);

        // PvPoke lists these first, but the Pokédex line is the usual form.
        Assert.True(Form("zacian_hero").IsDefault);
        Assert.Equal(FormKind.Alternate, Form("zacian_crowned_sword").Kind);
        Assert.True(Form("darmanitan_standard").IsDefault);
        Assert.Equal(FormKind.Regional, Form("darmanitan_galarian_zen").Kind);

        Assert.All(_data.Forms.GroupBy(form => form.Dex), group => Assert.Single(group, form => form.IsDefault));
        Assert.DoesNotContain(_data.Forms, form => form.Id.EndsWith("_shadow", StringComparison.Ordinal));
    }

    [Fact]
    public void Forms_StatsTypesAndFlags()
    {
        var charizard = Form("charizard");
        Assert.Equal((223, 173, 186, "fire", "flying"), (charizard.Attack, charizard.Defense, charizard.Stamina, charizard.Type1Id, charizard.Type2Id));
        Assert.Equal(3266, charizard.MaxCp50);
        Assert.True(charizard.Released);
        Assert.True(charizard.ShadowReleased);
        Assert.True(charizard.CanDynamax);
        Assert.True(charizard.CanGigantamax);
        Assert.Null(charizard.ShinyReleased);
        Assert.Equal((3, 10000), (charizard.BuddyDistanceKm, charizard.ThirdMoveStardust));

        Assert.False(Form("charizard_mega_x").ShadowReleased);
        Assert.False(Form("charizard_mega_x").CanDynamax);
        Assert.Null(Form("zacian_hero").Type2Id);
        Assert.False(Form("vulpix_alolan").CanGigantamax);
    }

    [Fact]
    public void Moves_FastAndChargedWithRaidAndPvpNumbers()
    {
        var vineWhip = Move("VINE_WHIP");
        Assert.Equal(MoveKind.Fast, vineWhip.Kind);
        Assert.True(vineWhip.RaidDurationMs > 0);
        Assert.True(vineWhip.PvpEnergyDelta > 0);
        Assert.NotNull(vineWhip.PvpTurns);

        var blastBurn = Move("BLAST_BURN");
        Assert.Equal((MoveKind.Charged, "fire"), (blastBurn.Kind, blastBurn.TypeId));
        Assert.True(blastBurn.RaidEnergyDelta < 0);
        Assert.True(blastBurn.PvpEnergyDelta < 0);
        Assert.Null(blastBurn.PvpTurns);

        // Hidden Power has no raid version in the game master, and that is no warning.
        var hiddenPower = Move("HIDDEN_POWER_PSYCHIC");
        Assert.Equal((0d, 0), (hiddenPower.RaidPower, hiddenPower.RaidDurationMs));
        Assert.DoesNotContain(_data.Warnings, warning => warning.Contains("HIDDEN_POWER", StringComparison.Ordinal));
    }

    [Fact]
    public void Movepools_HaveEliteTmMovesAndPvpokesExtraMegaMoves()
    {
        var charizard = _data.FormMoves.Where(m => m.FormId == "charizard").ToDictionary(m => m.MoveId, m => m.Availability);
        Assert.Equal(MoveAvailability.EliteTm, charizard["BLAST_BURN"]);
        Assert.Equal(MoveAvailability.Normal, charizard["FIRE_SPIN"]);
        Assert.Contains(_data.FormMoves, m => m.FormId == "beedrill_mega" && m.MoveId == "FELL_STINGER_PLUS");
        Assert.All(_data.FormMoves, m => Assert.Contains(_data.Moves, move => move.Id == m.MoveId));
        Assert.Equal(_data.FormMoves.Count, _data.FormMoves.Select(m => (m.FormId, m.MoveId)).Distinct().Count());
    }

    [Fact]
    public void Evolutions_CandyItemsLuresTimeAndTasks()
    {
        Assert.Equal(25, Evolution("bulbasaur", "ivysaur").Candy);
        Assert.Equal(100, Evolution("ivysaur", "venusaur").Candy);

        var espeon = Evolution("eevee", "espeon");
        Assert.Equal(("day", 10), (espeon.TimeOfDay, espeon.BuddyDistanceKm));
        Assert.Equal("Walk 10 km with it as your buddy", espeon.TaskEn);
        Assert.Equal("night", Evolution("eevee", "umbreon").TimeOfDay);
        Assert.Equal("ITEM_TROY_DISK_MOSSY", Evolution("eevee", "leafeon").LureId);
        Assert.Equal("Earn 70 hearts with it as your buddy", Evolution("eevee", "sylveon").TaskEn);
        Assert.Null(Evolution("eevee", "vaporeon").TaskEn);
        Assert.Equal("ninetales_alolan", Assert.Single(_data.Evolutions, e => e.FromFormId == "vulpix_alolan").ToFormId);

        var gholdengo = Evolution("gimmighoul", "gholdengo");
        Assert.Equal(("ITEM_OTHER_EVOLUTION_STONE_A", "Uses 999 Gimmighoul Coins"), (gholdengo.ItemId, gholdengo.TaskEn));

        // A Mega Evolution is a form of its own, not an evolution step.
        Assert.DoesNotContain(_data.Evolutions, e => e.ToFormId.Contains("_mega", StringComparison.Ordinal));
        Assert.All(_data.Evolutions, e => Assert.NotEqual(e.FromFormId, e.ToFormId));
    }

    [Fact]
    public void CpMultipliers_EveryHalfLevelUpToFiftyOne()
    {
        Assert.Equal(101, _data.CpMultipliers.Count);
        Assert.Equal(1m, _data.CpMultipliers[0].Level);
        Assert.Equal(51m, _data.CpMultipliers[^1].Level);
        Assert.Equal(0.84029999, _data.CpMultipliers.Single(m => m.Level == 50m).Value, 6);
    }

    [Fact]
    public void Rankings_PerLeague_WithShadowsAndPaddedMovesets()
    {
        foreach (var league in new[] { League.Great, League.Ultra, League.Master })
        {
            var rows = _data.PvpRankings.Where(r => r.League == league).ToList();
            Assert.NotEmpty(rows);
            Assert.Equal(rows.Count, rows.Select(r => (r.FormId, r.IsShadow)).Distinct().Count());
            Assert.Equal(1, rows.Min(r => r.Rank));
        }

        Assert.Contains(_data.PvpRankings, r => r.IsShadow && r.FormId == "venusaur");
        var unown = Assert.Single(_data.PvpRankings, r => r.FormId == "unown" && r.League == League.Great);
        Assert.Equal(("HIDDEN_POWER_PSYCHIC", "STRUGGLE", null), (unown.FastMoveId, unown.ChargedMove1Id, unown.ChargedMove2Id));
    }

    [Fact]
    public void MoveUsage_PerRankedForm_FastAndCharged_WithPvpokesCounts()
    {
        var ninetales = _data.PvpMoveUsage
            .Where(u => u.League == League.Great && u.FormId == "ninetales" && u.IsShadow)
            .ToDictionary(u => u.MoveId, u => u.Uses);
        Assert.Equal(63207, ninetales["EMBER"]);
        Assert.Equal(38719, ninetales["WEATHER_BALL_FIRE"]);
        // Only for ranked forms and known moves, once per move.
        var ranked = _data.PvpRankings.Select(r => (r.League, r.FormId, r.IsShadow)).ToHashSet();
        var moves = _data.Moves.Select(m => m.Id).ToHashSet();
        Assert.All(_data.PvpMoveUsage, u => Assert.True(ranked.Contains((u.League, u.FormId, u.IsShadow)) && moves.Contains(u.MoveId)));
        Assert.Equal(_data.PvpMoveUsage.Count, _data.PvpMoveUsage.Select(u => (u.League, u.FormId, u.IsShadow, u.MoveId)).Distinct().Count());
    }

    [Fact]
    public void Warnings_NameWhatTheGameMasterDoesntHave()
    {
        Assert.Contains("2 forms not found in the game master (no evolutions or Dynamax for them yet): basculegion_female, basculegion_male", _data.Warnings);
        Assert.Contains(_data.Warnings, warning => warning.Contains("FELL_STINGER_PLUS", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_LeavesOutUnknownTypesMovesAndRankingRows_WithWarnings()
    {
        var pvpoke = new PvpokeGameMaster
        {
            Pokemon =
            [
                Pokemon(1, "bulbasaur", ["grass", "poison"], fast: ["TACKLE"], charged: ["SLUDGE_BOMB", "MISSING_MOVE"], legacy: ["SLUDGE_BOMB"]),
                Pokemon(1, "bulbasaur_shadow", ["grass", "poison"], fast: ["TACKLE"], charged: ["SLUDGE_BOMB"], released: false),
                Pokemon(2, "strange", ["cosmic"], fast: ["TACKLE"], charged: ["SLUDGE_BOMB"]),
            ],
            Moves =
            [
                new PvpokeMove { MoveId = "TACKLE", Name = "Tackle", Type = "normal", Power = 3, EnergyGain = 3, Turns = 1 },
                new PvpokeMove
                {
                    MoveId = "SLUDGE_BOMB", Name = "Sludge Bomb", Type = "poison", Power = 80, Energy = 50, Turns = 1,
                    Buffs = [-1, 0], BuffTarget = "self", BuffApplyChance = "0.3",
                },
                new PvpokeMove { MoveId = "COSMIC_RAY", Name = "Cosmic Ray", Type = "cosmic", Power = 1, Energy = 1 },
            ],
        };
        var gameMaster = GameMasterSource.Read(Fixtures.File("GAME_MASTER.json", "Game master"));
        var rankings = new List<(League, List<PvpokeRanking>)>
        {
            (League.Great, [new PvpokeRanking { SpeciesId = "bulbasaur", Moveset = ["TACKLE", "SLUDGE_BOMB"], Score = 80 },
                new PvpokeRanking { SpeciesId = "ghost", Moveset = ["TACKLE", "SLUDGE_BOMB"] }]),
        };

        var data = GameDataBuilder.Build(pvpoke, gameMaster, [(1m, 0.094), (50m, 0.8403)], rankings);

        Assert.Single(data.Forms);
        Assert.False(data.Forms[0].ShadowReleased);
        var sludgeBomb = Assert.Single(data.Moves, move => move.Id == "SLUDGE_BOMB");
        Assert.Equal((0.3, -1, 0), (sludgeBomb.PvpBuffChance, sludgeBomb.PvpSelfAttackStages, sludgeBomb.PvpOpponentAttackStages));
        Assert.Equal(MoveAvailability.Legacy, Assert.Single(data.FormMoves, m => m.MoveId == "SLUDGE_BOMB").Availability);
        Assert.Single(data.PvpRankings);
        Assert.Contains("1 forms with a type the import doesn't know, left out: strange", data.Warnings);
        Assert.Contains("1 moves with a type the import doesn't know, left out: COSMIC_RAY", data.Warnings);
        Assert.Contains("bulbasaur: moves not in PvPoke's move list, left out: MISSING_MOVE", data.Warnings);
        Assert.Contains("1 Great League ranking rows for unknown forms or moves, left out: ghost", data.Warnings);
    }

    [Fact]
    public void Warnings_ListAtMostTwentyNames()
    {
        var pvpoke = new PvpokeGameMaster
        {
            Pokemon = [.. Enumerable.Range(1, 25).Select(dex => Pokemon(dex, $"odd{dex}", ["cosmic"], ["TACKLE"], ["TACKLE"]))],
            Moves = [new PvpokeMove { MoveId = "TACKLE", Name = "Tackle", Type = "normal", Power = 3, EnergyGain = 3, Turns = 1 }],
        };

        var data = GameDataBuilder.Build(pvpoke, GameMasterSource.Read(Fixtures.File("GAME_MASTER.json", "Game master")), [(50m, 0.84)], []);

        Assert.Contains(data.Warnings, warning => warning.StartsWith("25 forms with a type", StringComparison.Ordinal) && warning.EndsWith("and 5 more", StringComparison.Ordinal));
    }

    [Fact]
    public void BothWaysBuff_AndMissingChance()
    {
        var pvpoke = new PvpokeGameMaster
        {
            Pokemon = [Pokemon(1, "bulbasaur", ["grass"], ["TACKLE"], ["OBSTRUCT"])],
            Moves =
            [
                new PvpokeMove { MoveId = "TACKLE", Name = "Tackle", Type = "normal", Power = 3, EnergyGain = 3, Turns = 0 },
                new PvpokeMove
                {
                    MoveId = "OBSTRUCT", Name = "Obstruct", Type = "dark", Power = 15, Energy = 40, BuffTarget = "both",
                    Buffs = [0, 1], BuffsSelf = [0, 1], BuffsOpponent = [0, -1], BuffApplyChance = "not a number",
                },
            ],
        };

        var data = GameDataBuilder.Build(pvpoke, GameMasterSource.Read(Fixtures.File("GAME_MASTER.json", "Game master")), [(50m, 0.84)], []);

        var obstruct = Assert.Single(data.Moves, move => move.Id == "OBSTRUCT");
        Assert.Equal((1, -1), (obstruct.PvpSelfDefenseStages, obstruct.PvpOpponentDefenseStages));
        Assert.Null(obstruct.PvpBuffChance);
        Assert.Equal(1, Assert.Single(data.Moves, move => move.Id == "TACKLE").PvpTurns);
    }

    [Theory]
    [InlineData("Giratina (Altered)", "Giratina")]
    [InlineData("Charizard", "Charizard")]
    [InlineData("Mr. Mime (Galarian)", "Mr. Mime")]
    public void SpeciesName_DropsTheFormInBrackets(string name, string expected) =>
        Assert.Equal(expected, GameDataBuilder.SpeciesName(name));

    [Fact]
    public void Cp_IsAtLeastTen() => Assert.Equal(10, GameDataBuilder.Cp(new PvpokeStats { Atk = 1, Def = 1, Hp = 1 }, 0.094));

    [Fact]
    public void RaidNumbers_UseTheGameMastersSpellings()
    {
        var gameMaster = GameMasterSource.Read(Fixtures.File("GAME_MASTER.json", "Game master"));
        Assert.NotNull(GameDataBuilder.RaidNumbers("VINE_WHIP", fast: true, gameMaster));
        Assert.Null(GameDataBuilder.RaidNumbers("VINE_WHIP", fast: false, gameMaster));
        Assert.Null(GameDataBuilder.RaidNumbers("AEGISLASH_CHARGE_PSYCHO_CUT", fast: false, gameMaster));
    }

    [Fact]
    public void MainForm_FallsBackToPvpokesFirst()
    {
        var gameMaster = GameMasterSource.Read(Fixtures.File("GAME_MASTER.json", "Game master"));
        var mega = Pokemon(9999, "nothing_mega", ["fire"], [], [], tags: ["mega"]);
        var plain = Pokemon(9999, "nothing", ["fire"], [], []);

        Assert.Same(mega, GameDataBuilder.MainForm([mega], gameMaster));
        Assert.Same(plain, GameDataBuilder.MainForm([mega, plain], gameMaster));
    }

    private static PvpokePokemon Pokemon(
        int dex, string id, List<string> types, List<string> fast, List<string> charged,
        List<string>? legacy = null, bool released = true, List<string>? tags = null) => new()
        {
            Dex = dex,
            SpeciesId = id,
            SpeciesName = id,
            BaseStats = new PvpokeStats { Atk = 100, Def = 100, Hp = 100 },
            Types = types,
            FastMoves = fast,
            ChargedMoves = charged,
            LegacyMoves = legacy ?? [],
            Tags = tags ?? [],
            Released = released,
        };
}
