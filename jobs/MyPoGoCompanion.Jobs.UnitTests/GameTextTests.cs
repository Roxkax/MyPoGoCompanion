using System.Text;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

public sealed class GameTextTests
{
    internal static SourceFile Text(string json, string name = "Portuguese game text") =>
        new(name, new Uri("https://example.test/" + Guid.NewGuid()), Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Read_KeysAndTexts_LaterFilesWin()
    {
        var text = GameText.Read(
            Text("""{"data": ["move_name_0013", "Embrulho", "pokemon_type_fire", "Fogo"]}"""),
            null,
            Text("""{"data": ["move_name_0013", "Embrulho novo"]}"""));

        Assert.Equal(2, text.Count);
        Assert.Equal(("Embrulho novo", "Fogo"), (text.Get("move_name_0013"), text.Get("pokemon_type_fire")));
    }

    [Fact]
    public void Get_ResolvesReferences_AndTrims()
    {
        var text = GameText.Read(Text("""
            {"data": [
              "move_name_0296", "Surfar",
              "move_name_0498", "<<move_name_0296>>+",
              "move_name_0600", "<<move_name_0498>> e <<move_name_0296>>",
              "pokemon_name_0892_2724_3", "Urshifu Estilo Golpe Decisivo Gigamax "
            ]}
            """));

        Assert.Equal("Surfar+", text.Get("move_name_0498"));
        Assert.Equal("Surfar+ e Surfar", text.Get("move_name_0600"));
        Assert.Equal("Urshifu Estilo Golpe Decisivo Gigamax", text.Get("pokemon_name_0892_2724_3"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("form_normal")]
    [InlineData("broken")]
    [InlineData("loop_a")]
    public void Get_MissingEmptyOrUnresolvable_IsNull(string key)
    {
        var text = GameText.Read(Text("""
            {"data": ["form_normal", " ", "broken", "<<nowhere>>+", "loop_a", "<<loop_b>>", "loop_b", "<<loop_a>>"]}
            """));

        Assert.Null(text.Get(key));
    }

    [Theory]
    [InlineData("""{"data": ["only a key"]}""", "isn't a list of resource ids and texts")]
    [InlineData("""{"texts": []}""", "isn't a list of resource ids and texts")]
    [InlineData("""["a", "b"]""", "isn't a list of resource ids and texts")]
    [InlineData("""{"data": [""", "isn't the JSON the import expects")]
    public void Read_SomethingElse_IsAnImportProblem(string json, string expected)
    {
        var exception = Assert.Throws<ImportException>(() => GameText.Read(Text(json)));

        Assert.Contains("Portuguese game text " + expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_NonTextValues_AreKeptAsText() =>
        Assert.Equal("5", GameText.Read(Text("""{"data": ["number", 5]}""")).Get("number"));
}
