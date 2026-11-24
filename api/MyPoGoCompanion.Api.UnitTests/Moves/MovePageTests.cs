using MyPoGoCompanion.Api.Moves;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Api.UnitTests.Moves;

public sealed class MovePageTests
{
    [Fact]
    public void Learners_SortByAvailabilityAndAttack_AndOnlyFoldMegasWithMatchingAvailability()
    {
        var baseForm = Learner("base", 250, MoveAvailability.Normal);
        var mega = Learner("mega", 340, MoveAvailability.Normal, FormKind.Mega, "base");
        var otherMega = Learner("mega-elite", 350, MoveAvailability.EliteTm, FormKind.Mega, "base");
        var weaker = Learner("weaker", 190, MoveAvailability.Normal);
        var legacy = Learner("legacy", 100, MoveAvailability.Legacy);

        var learners = MovePageEndpoints.Learners([weaker, mega, legacy, baseForm, otherMega]);
        Assert.Equal(["mega-elite", "legacy", "base", "weaker"], learners.Select(row => row.FormId));
        Assert.True(learners[2].MegaToo);
        Assert.False(learners[0].MegaToo);
    }

    private static FormMove Learner(string id, int attack, MoveAvailability availability, FormKind kind = FormKind.Base, string? baseId = null) =>
        new()
        {
            FormId = id,
            MoveId = "EARTH_POWER",
            Availability = availability,
            Form = new Form
            {
                Id = id,
                Kind = kind,
                BaseFormId = baseId,
                NameEn = id,
                Type1Id = "ground",
                Attack = attack,
                Species = new Species { NameEn = "Example", FamilyId = "example" },
            },
        };
}
