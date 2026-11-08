using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Api.UnitTests.Pokedex;

public class PokedexNamesTests
{
    [Theory]
    [InlineData("Charizard (Mega X)", false, "Mega X")]
    [InlineData("Vulpix (Alolan)", false, "Alolan")]
    [InlineData("Giratina (Origin)", false, "Origin")]
    [InlineData("Oricorio (Pa'u)", false, "Pa'u")]
    [InlineData("Darmanitan (Standard)", true, null)]
    [InlineData("Charizard", false, null)]
    public void LabelEn_IsTheTextInBrackets_ExceptForTheMainForm(string name, bool isDefault, string? expected) =>
        Assert.Equal(expected, PokedexNames.LabelEn(name, isDefault));

    [Theory]
    [InlineData("Charizard (Mega X)", "Charizard", FormKind.Mega, "Mega Charizard X")]
    [InlineData("Venusaur (Mega)", "Venusaur", FormKind.Mega, "Mega Venusaur")]
    [InlineData("Kyogre (Primal)", "Kyogre", FormKind.Primal, "Primal Kyogre")]
    [InlineData("Arcanine (Hisuian)", "Arcanine", FormKind.Regional, "Hisuian Arcanine")]
    [InlineData("Darmanitan (Galarian Zen)", "Darmanitan", FormKind.Regional, "Galarian Darmanitan (Zen)")]
    [InlineData("Giratina (Origin)", "Giratina", FormKind.Alternate, "Giratina (Origin)")]
    [InlineData("Snorlax", "Snorlax", FormKind.Base, "Snorlax")]
    public void DisplayNameEn_PutsMegaAndRegionBeforeTheName(string name, string species, FormKind kind, string expected) =>
        Assert.Equal(expected, PokedexNames.DisplayNameEn(name, species, kind, isDefault: false));

    [Fact]
    public void DisplayNameEn_MainForm_KeepsItsName() =>
        Assert.Equal("Zacian (Hero)", PokedexNames.DisplayNameEn("Zacian (Hero)", "Zacian", FormKind.Base, isDefault: true));

    [Theory]
    [InlineData("Mega Charizard X", "Charizard", "Mega X")]
    [InlineData("Mega Venusaur", "Venusaur", "Mega")]
    [InlineData("Kyogre Primitivo", "Kyogre", "Primitivo")]
    [InlineData("Vulpix de Alola", "Vulpix", "Alola")]
    [InlineData("Giratina (Forma Origem)", "Giratina", "Forma Origem")]
    [InlineData("Kyurem Branco", "Kyurem", "Branco")]
    [InlineData("Necrozma Asas do Alvorecer", null, null)]
    [InlineData("Kyurem", "Kyurem", null)]
    [InlineData("Outro Nome", "Kyurem", null)]
    [InlineData(null, "Kyurem", null)]
    [InlineData(" ", "Kyurem", null)]
    public void LabelPt_IsTheGameNameWithoutTheSpecies(string? name, string? species, string? expected) =>
        Assert.Equal(expected, PokedexNames.LabelPt(name, species, isDefault: false));

    [Fact]
    public void LabelPt_MainForm_HasNone() =>
        Assert.Null(PokedexNames.LabelPt("Giratina (Forma Alterada)", "Giratina", isDefault: true));
}
