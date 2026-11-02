using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

public sealed class EvolutionTaskTests
{
    private static GameMasterEvolution Branch(
        string? item = null, int? itemCount = null, int? km = null, bool buddy = false, bool fullMoon = false,
        bool upsideDown = false, bool dusk = false, string? move = null) =>
        new("TARGET", 25, false, item, itemCount, km, buddy, null, null, null, null, fullMoon, upsideDown, dusk, move);

    private static GameMasterQuest Quest(string type, int target, string[]? types = null, string[]? conditions = null, string? throwType = null) =>
        new("Q", type, target, types ?? [], conditions ?? [], throwType);

    [Theory]
    [InlineData("QUEST_CATCH_POKEMON", 20, new[] { "fighting" }, new string[0], null, "Catch 20 Fighting-type Pokémon")]
    [InlineData("QUEST_CATCH_POKEMON", 5, new string[0], new string[0], null, "Catch 5 Pokémon")]
    [InlineData("QUEST_FIGHT_POKEMON", 30, new[] { "ghost", "psychic" }, new string[0], null, "Defeat 30 Ghost or Psychic-type Pokémon")]
    [InlineData("QUEST_COMPLETE_RAID_BATTLE", 10, new string[0], new string[0], null, "Win 10 raids")]
    [InlineData("QUEST_COMPLETE_BATTLE", 15, new[] { "dark" }, new[] { "COMBAT_TYPE_RAID" }, null, "Win 15 raids with Dark-type Pokémon")]
    [InlineData("QUEST_COMPLETE_BATTLE", 200, new string[0], new[] { "COMBAT_TYPE_RAID", "COMBAT_TYPE_DMAX" }, null, "Win 200 raids or Max Battles")]
    [InlineData("QUEST_COMPLETE_BATTLE", 3, new string[0], new string[0], null, "Win 3 battles")]
    [InlineData("QUEST_BUDDY_EVOLUTION_WALK", 10, new string[0], new string[0], null, "Walk 10 km with it as your buddy")]
    [InlineData("QUEST_BUDDY_EARN_AFFECTION_POINTS", 70, new string[0], new string[0], null, "Earn 70 hearts with it as your buddy")]
    [InlineData("QUEST_BUDDY_FEED", 25, new string[0], new string[0], null, "Feed it 25 treats as your buddy")]
    [InlineData("QUEST_USE_INCENSE", 1, new string[0], new string[0], null, "Use an Incense")]
    [InlineData("QUEST_USE_INCENSE", 3, new string[0], new string[0], null, "Use 3 Incense")]
    [InlineData("QUEST_LAND_THROW", 10, new string[0], new string[0], "ACTIVITY_CATCH_EXCELLENT_THROW", "Make 10 Excellent Throws")]
    [InlineData("QUEST_LAND_THROW", 10, new string[0], new string[0], "ACTIVITY_CATCH_GREAT_THROW", "Make 10 Great Throws")]
    [InlineData("QUEST_LAND_THROW", 10, new string[0], new string[0], "ACTIVITY_CATCH_NICE_THROW", "Make 10 Nice Throws")]
    [InlineData("QUEST_LAND_THROW", 10, new string[0], new string[0], null, "Make 10 throws")]
    [InlineData("QUEST_SOMETHING_NEW", 1, new string[0], new string[0], null, "Complete a special task")]
    public void Quests_ReadAsEnglish(string type, int target, string[] types, string[] conditions, string? throwType, string expected) =>
        Assert.Equal(expected, EvolutionTask.Describe(Quest(type, target, types, conditions, throwType)));

    [Fact]
    public void NothingSpecial_IsNoText() => Assert.Null(EvolutionTask.Describe(Branch(item: "ITEM_SUN_STONE", itemCount: 1, km: 3), null));

    [Fact]
    public void EveryExtra_IsListed() =>
        Assert.Equal(
            "As your buddy; Uses 50 Zygarde Cells; Must know Dragon Ascent; Only during a full moon; Only at dusk; Turn your phone upside down",
            EvolutionTask.Describe(Branch(item: "ITEM_BEANS", itemCount: 50, buddy: true, fullMoon: true, upsideDown: true, dusk: true, move: "DRAGON_ASCENT"), null));

    [Fact]
    public void AQuest_ReplacesAsYourBuddy() =>
        Assert.Equal("Walk 1 km with it as your buddy; Uses 3 Linking Cord",
            EvolutionTask.Describe(Branch(item: "ITEM_LINKING_CORD", itemCount: 3, buddy: true), Quest("QUEST_BUDDY_EVOLUTION_WALK", 1)));

    [Fact]
    public void BuddyWithADistance_IsInTheDistanceColumn() => Assert.Null(EvolutionTask.Describe(Branch(km: 10, buddy: true), null));

    [Theory]
    [InlineData("DRAGON_ASCENT", "Dragon Ascent")]
    [InlineData("fighting", "Fighting")]
    [InlineData("A__B", "A B")]
    public void Words_TitleCase(string id, string expected) => Assert.Equal(expected, EvolutionTask.Words(id));

    [Fact]
    public void Describe_NeedsABranchAndAQuest()
    {
        Assert.Throws<ArgumentNullException>(() => EvolutionTask.Describe(null!, null));
        Assert.Throws<ArgumentNullException>(() => EvolutionTask.Describe((GameMasterQuest)null!));
    }
}
