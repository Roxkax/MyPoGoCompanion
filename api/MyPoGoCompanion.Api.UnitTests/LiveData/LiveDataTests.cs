using MyPoGoCompanion.Api.LiveData;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Api.UnitTests.LiveData;

public sealed class LiveDataTests
{
    [Theory]
    [InlineData("gengar_mega", FormKind.Mega, "gengar", "gengar")]
    [InlineData("kyogre_primal", FormKind.Primal, "kyogre", "kyogre")]
    [InlineData("gengar_mega", FormKind.Mega, null, null)]
    [InlineData("necrozma_dawn_wings", FormKind.Alternate, null, "necrozma")]
    [InlineData("necrozma_dusk_mane", FormKind.Alternate, null, "necrozma")]
    [InlineData("kyurem_black", FormKind.Alternate, null, "kyurem")]
    [InlineData("kyurem_white", FormKind.Alternate, null, "kyurem")]
    [InlineData("zacian_crowned_sword", FormKind.Alternate, null, "zacian_hero")]
    [InlineData("zamazenta_crowned_shield", FormKind.Alternate, null, "zamazenta_hero")]
    [InlineData("giratina_origin", FormKind.Alternate, null, "giratina_origin")]
    [InlineData("vulpix_alolan", FormKind.Regional, null, "vulpix_alolan")]
    public void CatchFormId_UsesTheRewardForm(string id, FormKind kind, string? baseId, string? expected)
    {
        var form = new Form { Id = id, NameEn = id, Type1Id = "water", Kind = kind, BaseFormId = baseId };
        Assert.Equal(expected, LiveDataEndpoints.CatchFormId(form));
    }

    [Fact]
    public void PerfectCp_UsesTheExactMultiplierForTheEncounter_AndReturnsNullWithoutData()
    {
        var gible = new Form { Id = "gible", NameEn = "Gible", Type1Id = "dragon", Attack = 124, Defense = 84, Stamina = 151 };
        var multipliers = new Dictionary<decimal, double> { [15m] = 0.51739395, [20m] = 0.59740001, [25m] = 0.667934 };
        Assert.Equal(477, LiveDataEndpoints.PerfectCp(gible, 15m, multipliers));
        Assert.Equal(635, LiveDataEndpoints.PerfectCp(gible, 20m, multipliers));
        Assert.Equal(794, LiveDataEndpoints.PerfectCp(gible, 25m, multipliers));
        Assert.Null(LiveDataEndpoints.PerfectCp(gible, 50m, multipliers));
        Assert.Null(LiveDataEndpoints.PerfectCp(null, 15m, multipliers));
    }
}
