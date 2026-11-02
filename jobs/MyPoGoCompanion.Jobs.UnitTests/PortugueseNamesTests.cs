using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>The Portuguese names (E02-F02) from the trimmed real game text, and the rules on made-up text.</summary>
public sealed class PortugueseNamesTests
{
    private static readonly ImportedGameData _data = Fixtures.Built;

    private static readonly GameMasterSource _gameMaster = GameMasterSource.Read(Fixtures.File("GAME_MASTER.json", "Game master"));

    private static readonly Dictionary<string, string> _typeNames = new(StringComparer.Ordinal) { ["fire"] = "Fogo", ["normal"] = "Normal" };

    private static string? FormPt(string id) => Assert.Single(_data.Forms, form => form.Id == id).NamePt;

    private static string? MovePt(string id) => Assert.Single(_data.Moves, move => move.Id == id).NamePt;

    private static GameText Text(string json) => GameText.Read(GameTextTests.Text(json));

    [Fact]
    public void SpeciesAndTypes_TheGamesNames()
    {
        Assert.Equal("Charizard", Assert.Single(_data.Species, s => s.Dex == 6).NamePt);
        Assert.Equal(_data.Species.Count, _data.Species.Count(s => s.NamePt is not null));
        Assert.Equal(("Planta", "Fada"), (_data.Types[4].NamePt, _data.Types[17].NamePt));
    }

    [Theory]
    [InlineData("charizard", "Charizard")]
    [InlineData("charizard_mega_x", "Mega Charizard X")]
    [InlineData("charizard_mega_y", "Mega Charizard Y")]
    [InlineData("beedrill_mega", "Mega Beedrill")]
    [InlineData("vulpix_alolan", "Vulpix de Alola")]
    [InlineData("castform_sunny", "Castform (Forma Ensolarada)")]
    [InlineData("darmanitan_zen", "Darmanitan (Modo Zen)")]
    [InlineData("darmanitan_galarian_zen", "Darmanitan de Galar (Modo Zen)")]
    [InlineData("zacian_crowned_sword", "Zacian (Espada Coroada)")]
    [InlineData("basculegion_female", "Basculegion (Fêmea)")]
    [InlineData("unown", "Unown")]
    public void Forms_NamedTheWayTheGameDoes(string id, string expected) => Assert.Equal(expected, FormPt(id));

    [Fact]
    public void Moves_ByTheGamesNumber_AndPvpokesOwnMovesFromTheirBase()
    {
        Assert.Equal("Chama Furacão", MovePt("FIRE_SPIN"));
        Assert.Equal("Poder Oculto (Fogo)", MovePt("HIDDEN_POWER_FIRE"));
        Assert.Equal(_data.Moves.Count, _data.Moves.Count(m => m.NamePt is not null));
        Assert.DoesNotContain(_data.Warnings, warning => warning.Contains("Portuguese", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SURF_PLUS", false, "Surfar+")]
    [InlineData("HIDDEN_POWER_NORMAL", true, "Poder Oculto")]
    [InlineData("HIDDEN_POWER_FIRE", true, "Poder Oculto (Fogo)")]
    [InlineData("HIDDEN_POWER_FAIRY", true, null)]
    [InlineData("AEGISLASH_CHARGE_SURF", false, "Surfar")]
    [InlineData("WRAP", false, "Embrulho")]
    [InlineData("GULP_MISSILE_ARROKUDA", false, null)]
    [InlineData("NOT_A_MOVE_PLUS", false, null)]
    public void MoveName_Rules(string id, bool fast, string? expected)
    {
        var gameMaster = GameMasterSource.Read(GameTextTests.Text("""
            [
              {"templateId": "V0013_MOVE_WRAP", "data": {"moveSettings": {"power": 60}}},
              {"templateId": "V0057_MOVE_SURF", "data": {"moveSettings": {"power": 65}}},
              {"templateId": "V0281_MOVE_HIDDEN_POWER_FAST", "data": {"moveSettings": {"power": 15}}},
              {"templateId": "V0001_POKEMON_BULBASAUR", "data": {"pokemonSettings": {"pokemonId": "BULBASAUR"}}}
            ]
            """, "Game master"));
        var text = Text("""{"data": ["move_name_0013", "Embrulho", "move_name_0057", "Surfar", "move_name_0281", "Poder Oculto"]}""");

        Assert.Equal(expected, PortugueseNames.MoveName(id, fast, text, gameMaster, _typeNames));
    }

    [Fact]
    public void FormName_AFormTextNamingThePokemon_IsTheName()
    {
        var gameMaster = GameMasterSource.Read(GameTextTests.Text("""
            [
              {"templateId": "V0646_POKEMON_KYUREM_WHITE", "data": {"pokemonSettings": {"pokemonId": "KYUREM", "form": "KYUREM_WHITE"}}},
              {"templateId": "V0013_MOVE_WRAP", "data": {"moveSettings": {"power": 60}}}
            ]
            """, "Game master"));
        var text = Text("""{"data": ["form_white", "Kyurem Branco"]}""");
        var form = new Form { Id = "kyurem_white", Dex = 646, Kind = FormKind.Alternate, NameEn = "Kyurem (White)", GameMasterId = "KYUREM_WHITE", Type1Id = "dragon" };

        Assert.Equal("Kyurem Branco", PortugueseNames.FormName(form, "Kyurem", 3, text, gameMaster));
    }

    [Fact]
    public void FormName_WithoutAnyText_IsNull()
    {
        var text = Text("""{"data": []}""");
        Form Make(string id, FormKind kind, string name) => new() { Id = id, Dex = 25, Kind = kind, NameEn = name, Type1Id = "electric" };

        Assert.Null(PortugueseNames.FormName(Make("pikachu", FormKind.Base, "Pikachu"), null, 2, text, _gameMaster));
        Assert.Null(PortugueseNames.FormName(Make("pikachu_mega", FormKind.Mega, "Pikachu (Mega)"), "Pikachu", 2, text, _gameMaster));
        Assert.Null(PortugueseNames.FormName(Make("pikachu_libre", FormKind.Alternate, "Pikachu (Libre)"), "Pikachu", 2, text, _gameMaster));
        Assert.Null(PortugueseNames.FormName(Make("pikachu_alolan_x", FormKind.Regional, "Pikachu (Alolan X)"), "Pikachu", 2, text, _gameMaster));
        Assert.Null(PortugueseNames.FormLabel("NOT_IN_THE_GAME_MASTER", text, _gameMaster));
    }

    [Fact]
    public void Missing_NamesAreListed_AndLeftNull()
    {
        var data = GameDataBuilder.Build(Fixtures.Sources() with { GameText = GameTextTests.Text("""{"data": ["pokemon_name_0001", "Bulbasaur"]}""") });

        Assert.Equal("Bulbasaur", Assert.Single(data.Species, s => s.Dex == 1).NamePt);
        Assert.Null(Assert.Single(data.Species, s => s.Dex == 6).NamePt);
        Assert.All(data.Moves, move => Assert.Null(move.NamePt));
        Assert.Equal("Fogo", Assert.Single(data.Types, t => t.Id == "fire").NamePt);
        var species = Assert.Single(data.Warnings, w => w.Contains("species without a Portuguese name", StringComparison.Ordinal));
        Assert.StartsWith($"{data.Species.Count - 1} species without a Portuguese name in the game text (the app shows the English name): #2 Ivysaur", species, StringComparison.Ordinal);
        Assert.EndsWith("#999 Gimmighoul, #1000 Gholdengo", species, StringComparison.Ordinal);
        // Bulbasaur's forms: only those named in brackets that the text can't name are listed.
        Assert.DoesNotContain(data.Warnings, w => w.Contains("forms without", StringComparison.Ordinal) && w.Contains("bulbasaur", StringComparison.Ordinal));
        // A long list is cut after 30.
        var moves = Assert.Single(data.Warnings, w => w.Contains("moves without a Portuguese name", StringComparison.Ordinal));
        Assert.EndsWith($" and {data.Moves.Count - 30} more", moves, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithoutTheGameText_LeavesTheNamesNull_AndSaysWhy()
    {
        var data = GameDataBuilder.Build(Fixtures.Sources() with
        {
            GameText = null,
            GameTextRemote = null,
            GameTextProblems = ["Couldn't download the Portuguese game text (https://example.test/x): the server answered 404 Not Found."],
        });

        Assert.All(data.Species, s => Assert.Null(s.NamePt));
        Assert.All(data.Forms, f => Assert.Null(f.NamePt));
        Assert.Equal("Fada", data.Types[17].NamePt);
        Assert.Contains("Portuguese names not updated, the last ones are kept: Couldn't download the Portuguese game text (https://example.test/x): the server answered 404 Not Found.", data.Warnings);
    }

    [Fact]
    public void Build_ABrokenGameText_IsAWarning_NotAFailure()
    {
        var data = GameDataBuilder.Build(Fixtures.Sources() with { GameText = GameTextTests.Text("<html>rate limited</html>") });

        Assert.All(data.Moves, m => Assert.Null(m.NamePt));
        Assert.Contains(data.Warnings, w => w.StartsWith("Portuguese names not updated, the last ones are kept: The Portuguese game text isn't the JSON", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WithoutTheServerUpdate_UsesTheRelease()
    {
        var data = GameDataBuilder.Build(Fixtures.Sources() with { GameTextRemote = null, GameTextProblems = ["Couldn't download it."] });

        Assert.Equal("Vulpix de Alola", Assert.Single(data.Forms, f => f.Id == "vulpix_alolan").NamePt);
        Assert.Contains("Portuguese names from the game release only (the server's newer texts are missing): Couldn't download it.", data.Warnings);
    }
}
