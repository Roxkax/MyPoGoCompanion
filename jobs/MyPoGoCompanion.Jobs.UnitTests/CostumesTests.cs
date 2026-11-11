using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>Costumes from PokeMiners' picture names (E03-F02, decision 2026-11-08).</summary>
public sealed class CostumesTests
{
    private static readonly string[] _names =
    [
        "pm25.icon.png",
        "pm25.s.icon.png",
        "pm25.cFALL_2018.icon.png",
        "pm25.cFALL_2018.s.icon.png",
        "pm1.cJAN_2020_NOEVOLVE.icon.png",
        "pm12.cFASHION_2021_NOEVOLVE.g2.icon.png",
        "pm12.cFASHION_2021_NOEVOLVE.icon.png",
        "pm710.fSMALL.cFALL_2022.icon.png",
        "pm263.fGALARIAN.cGOFEST_2021_NOEVOLVE.s.icon.png",
        "pm263.fGALARIAN.cGOFEST_2021_NOEVOLVE.icon.png",
        "pm9999.cFALL_2018.icon.png",
        "pm25.cFALL_2018.portrait.png",
        "pm133.cMay_2023.icon.png",
    ];

    [Fact]
    public void Costumes_ComeFromCostumePictures_WithoutFemaleVariants_ForKnownSpecies()
    {
        var costumes = Costumes.FromFileNames(_names, new HashSet<int> { 1, 12, 25, 133, 263, 710 });

        Assert.Equal(
            ["pm1.cJAN_2020_NOEVOLVE", "pm12.cFASHION_2021_NOEVOLVE", "pm133.cMay_2023", "pm25.cFALL_2018", "pm263.fGALARIAN.cGOFEST_2021_NOEVOLVE", "pm710.fSMALL.cFALL_2022"],
            costumes.Select(costume => costume.Sprite));
        var pikachu = costumes.Single(costume => costume.Dex == 25);
        Assert.Equal(("FALL_2018", null, "Fall 2018", true), (pikachu.Code, pikachu.FormCode, pikachu.NameEn, pikachu.HasShinySprite));
        var pumpkaboo = costumes.Single(costume => costume.Dex == 710);
        Assert.Equal(("SMALL", "Fall 2022 (Small)", false), (pumpkaboo.FormCode, pumpkaboo.NameEn, pumpkaboo.HasShinySprite));
    }

    [Theory]
    [InlineData("JAN_2020_NOEVOLVE", null, "January 2020")]
    [InlineData("GOFEST_2021_NOEVOLVE", "GALARIAN", "GO Fest 2021 (Galarian)")]
    [InlineData("SPRING_2023_MYSTIC", null, "Spring 2023 Mystic")]
    [InlineData("ONE_YEAR_ANNIVERSARY", null, "One Year Anniversary")]
    [InlineData("NOEVOLVE", null, "Costume")]
    [InlineData("May_2023", null, "May 2023")]
    public void Names_AreMadeReadable(string code, string? form, string name) => Assert.Equal(name, Costumes.Name(code, form));

    [Fact]
    public void Add_UsesTheImportsSpecies()
    {
        var data = new ImportedGameData();
        data.Species.Add(new Data.GameData.Species { Dex = 25, NameEn = "Pikachu", FamilyId = "FAMILY_PIKACHU" });
        Costumes.Add(data, _names);
        Assert.Equal(["pm25.cFALL_2018"], data.Costumes.Select(costume => costume.Sprite));
    }
}
