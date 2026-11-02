using System.Text;
using System.Text.Json;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>Reading PvPoke's files and the game master; a file that doesn't read fails the run, with its name.</summary>
public sealed class SourceReadingTests
{
    private static SourceFile Text(string text, string name = "Test file") => new(name, new Uri("https://example.test/file"), Encoding.UTF8.GetBytes(text));

    [Fact]
    public void PvpokeGameMaster_ReadsPokemonAndMoves()
    {
        var gameMaster = PvpokeSource.ReadGameMaster(Fixtures.File("pvpoke-gamemaster.json", "PvPoke game master"));

        var charizard = Assert.Single(gameMaster.Pokemon, p => p.SpeciesId == "charizard");
        Assert.Equal((6, 223, 10000), (charizard.Dex, charizard.BaseStats.Atk, charizard.ThirdMoveCost));
        Assert.Contains("BLAST_BURN", charizard.EliteMoves);
        Assert.Equal("FAMILY_CHARMANDER", charizard.Family?.Id);
        Assert.Contains(gameMaster.Moves, m => m.MoveId == "FIRE_SPIN" && m.IsFast);
    }

    [Theory]
    [InlineData("{\"pokemon\": [], \"moves\": []}", "has no Pokémon or no moves")]
    [InlineData("{\"pokemon\": 5}", "isn't the JSON the import expects")]
    [InlineData("null", "is empty")]
    public void PvpokeGameMaster_RefusesWhatItCantRead(string json, string expected)
    {
        var exception = Assert.Throws<ImportException>(() => PvpokeSource.ReadGameMaster(Text(json, "PvPoke game master")));
        Assert.Contains("PvPoke game master", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rankings_ReadTheOrderScoreAndMoveset()
    {
        var rows = PvpokeSource.ReadRankings(Fixtures.File("pvpoke-rankings-1500.json", "PvPoke Great League rankings"));
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.Moveset.Count >= 2));
    }

    [Fact]
    public void CpMultipliers_ReadFromPokemonJs()
    {
        var values = PvpokeSource.ReadCpMultipliers(Fixtures.File("pvpoke-Pokemon.js", "PvPoke CP multipliers"), 51m);
        Assert.Equal(101, values.Count);
        Assert.Equal((1m, 0.094), (values[0].Level, Math.Round(values[0].Value, 3)));
        Assert.Equal(1.5m, values[1].Level);
    }

    [Theory]
    [InlineData("var other = [1];", "has no CP multiplier table")]
    [InlineData("var cpms = [0.094, abc];", "isn't a number: \"abc\"")]
    [InlineData("var cpms = [0.094, 0.135];", "only up to level 1.5; level 51 is needed")]
    public void CpMultipliers_RefuseABrokenTable(string script, string expected)
    {
        var exception = Assert.Throws<ImportException>(() => PvpokeSource.ReadCpMultipliers(Text(script, "PvPoke CP multipliers"), 51m));
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("10000", 10000)]
    [InlineData("false", 0)]
    [InlineData("null", 0)]
    [InlineData("\"25\"", 25)]
    [InlineData("\"lots\"", 0)]
    [InlineData("2.6", 3)]
    public void LenientInt_ReadsWhatPvpokeWrites(string value, int expected)
    {
        var pokemon = JsonSerializer.Deserialize<PvpokePokemon>($"{{\"thirdMoveCost\": {value}}}", JsonSerializerOptions.Web)!;
        Assert.Equal(expected, pokemon.ThirdMoveCost);
    }

    [Fact]
    public void LenientInt_WritesANumber() =>
        Assert.Contains("\"thirdMoveCost\":75000", JsonSerializer.Serialize(new PvpokePokemon { ThirdMoveCost = 75000 }, JsonSerializerOptions.Web), StringComparison.Ordinal);

    [Fact]
    public void GameMaster_ReadsTemplatesLooksMovesAndTasks()
    {
        var gameMaster = GameMasterSource.Read(Fixtures.File("GAME_MASTER.json", "Game master"));

        var vulpix = gameMaster.Pokemon["VULPIX_ALOLA"];
        Assert.Equal((37, "VULPIX", "VULPIX_ALOLA", "FAMILY_VULPIX"), (vulpix.Dex, vulpix.PokemonId, vulpix.Form, vulpix.FamilyId));
        Assert.Equal(["ice"], vulpix.Types);
        Assert.Equal("NINETALES_ALOLA", Assert.Single(vulpix.Evolutions).Target);
        Assert.Contains("CHARIZARD", gameMaster.CanGigantamax);
        Assert.Contains("CHARIZARD", gameMaster.CanDynamax);
        Assert.True(gameMaster.Moves["VINE_WHIP_FAST"].DurationMs > 0);
        Assert.Equal("QUEST_BUDDY_EVOLUTION_WALK", gameMaster.Quests["ESPEON_EVOLUTION_QUEST"].QuestType);
        Assert.Contains(gameMaster.PokemonByDex[6], t => t.Key == "CHARIZARD");

        // Charizard's Megas are temporary evolutions, not branches.
        Assert.DoesNotContain(gameMaster.Pokemon["CHARIZARD"].Evolutions, e => e.Target.Contains("MEGA", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{}", "isn't a list of templates")]
    [InlineData("[]", "has no Pokémon or no move templates")]
    [InlineData("[{\"templateId\": \"V0001_POKEMON_X\", \"data\": {\"pokemonSettings\": {\"stats\": {\"baseAttack\": \"x\"}}}}]", "has no Pokémon or no move templates")]
    [InlineData("[1, 2", "isn't the JSON the import expects")]
    public void GameMaster_RefusesWhatItCantRead(string json, string expected)
    {
        var exception = Assert.Throws<ImportException>(() => GameMasterSource.Read(Text(json, "Game master")));
        Assert.Contains("Game master", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GameMaster_SkipsTemplatesItDoesntUse()
    {
        const string json = """
            [
              {"templateId": "NO_DATA"},
              {"templateId": "V0001_POKEMON_BULBASAUR", "data": {"pokemonSettings": {"pokemonId": "BULBASAUR", "type": "POKEMON_TYPE_GRASS",
                "evolutionBranch": [{"evolution": "IVYSAUR", "candyCost": 25, "noCandyCostViaTrade": true, "genderRequirement": "FEMALE",
                  "evolutionItemRequirement": "ITEM_X", "evolutionItemRequirementCost": 3, "mustBeBuddy": true, "onlyFullMoon": true,
                  "onlyUpsideDown": true, "onlyDuskPeriod": true, "evolutionMoveRequirement": "TACKLE", "questDisplay": [{}]}]}}},
              {"templateId": "V0001_POKEMON_BULBASAUR", "data": {"pokemonSettings": {"pokemonId": "BULBASAUR"}}},
              {"templateId": "V0013_MOVE_WRAP", "data": {"moveSettings": {"power": 60, "durationMs": 2900}}},
              {"templateId": "EXT_1", "data": {"pokemonExtendedSettings": {"uniqueId": "BULBASAUR"}}},
              {"templateId": "EXT_2", "data": {"pokemonExtendedSettings": {"breadOverrides": [{"breadMode": "BREAD_MODE"}]}}},
              {"templateId": "EXT_3", "data": {"pokemonExtendedSettings": {"uniqueId": "BULBASAUR", "breadOverrides": [{"breadMode": "OTHER"}]}}},
              {"templateId": "Q_1", "data": {"evolutionQuestTemplate": {"questType": "QUEST_X"}}},
              {"templateId": "Q_2", "data": {"evolutionQuestTemplate": {"questTemplateId": "Q_2", "goals": [{"target": 3}, {"target": 5, "condition": [{"type": "WITH_THROW_TYPE", "withThrowType": {"throwType": "ACTIVITY_CATCH_GREAT_THROW"}}]}]}}}
            ]
            """;

        var gameMaster = GameMasterSource.Read(Text(json));

        var bulbasaur = Assert.Single(gameMaster.Pokemon).Value;
        Assert.Equal(0, bulbasaur.Attack);
        Assert.Equal(["grass"], bulbasaur.Types);
        var branch = Assert.Single(bulbasaur.Evolutions);
        Assert.Equal(new GameMasterEvolution("IVYSAUR", 25, true, "ITEM_X", 3, null, true, null, null, "female", null, true, true, true, "TACKLE"), branch);
        Assert.Empty(gameMaster.CanDynamax);
        var quest = Assert.Single(gameMaster.Quests).Value;
        Assert.Equal((5, "ACTIVITY_CATCH_GREAT_THROW", ""), (quest.Target, quest.ThrowType, quest.QuestType));
    }
}
