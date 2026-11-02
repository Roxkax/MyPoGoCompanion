using System.Globalization;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>
/// The English text for what an evolution needs beyond candy, an item, a buddy distance, a time of day, a lure or a
/// gender (those have their own columns): its special task ("Win 10 raids"), full moon, dusk, phone upside down, a
/// move it must know, and how many of an item it uses. There is no Portuguese version yet (the game text names things,
/// it doesn't describe these steps), so the app shows this English text.
/// </summary>
public static class EvolutionTask
{
    private static readonly Dictionary<string, string> _itemNames = new(StringComparer.Ordinal)
    {
        ["ITEM_BEANS"] = "Zygarde Cells",
        ["ITEM_OTHER_EVOLUTION_STONE_A"] = "Gimmighoul Coins",
    };

    public static string? Describe(GameMasterEvolution branch, GameMasterQuest? quest)
    {
        ArgumentNullException.ThrowIfNull(branch);
        var parts = new List<string>();
        if (quest is not null)
        {
            parts.Add(Describe(quest));
        }
        else if (branch.MustBeBuddy && branch.BuddyDistanceKm is null)
        {
            parts.Add("As your buddy");
        }

        if (branch.ItemId is not null && branch.ItemCount is > 1)
        {
            parts.Add($"Uses {branch.ItemCount.Value.ToString(CultureInfo.InvariantCulture)} {ItemName(branch.ItemId)}");
        }

        if (branch.MoveRequired is not null)
        {
            parts.Add($"Must know {Words(branch.MoveRequired)}");
        }

        if (branch.OnlyFullMoon)
        {
            parts.Add("Only during a full moon");
        }

        if (branch.OnlyDusk)
        {
            parts.Add("Only at dusk");
        }

        if (branch.OnlyUpsideDown)
        {
            parts.Add("Turn your phone upside down");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    public static string Describe(GameMasterQuest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var n = quest.Target.ToString(CultureInfo.InvariantCulture);
        var types = quest.Types.Count == 0 ? "" : string.Join(" or ", quest.Types.Select(Words)) + "-type ";
        return quest.QuestType switch
        {
            "QUEST_CATCH_POKEMON" => $"Catch {n} {types}Pokémon",
            "QUEST_FIGHT_POKEMON" => $"Defeat {n} {types}Pokémon",
            "QUEST_COMPLETE_RAID_BATTLE" => $"Win {n} raids",
            "QUEST_COMPLETE_BATTLE" when quest.Conditions.Contains("COMBAT_TYPE_DMAX") => $"Win {n} raids or Max Battles{TypeNote(quest)}",
            "QUEST_COMPLETE_BATTLE" when quest.Conditions.Contains("COMBAT_TYPE_RAID") => $"Win {n} raids{TypeNote(quest)}",
            "QUEST_COMPLETE_BATTLE" => $"Win {n} battles{TypeNote(quest)}",
            "QUEST_BUDDY_EVOLUTION_WALK" => $"Walk {n} km with it as your buddy",
            "QUEST_BUDDY_EARN_AFFECTION_POINTS" => $"Earn {n} hearts with it as your buddy",
            "QUEST_BUDDY_FEED" => $"Feed it {n} treats as your buddy",
            "QUEST_USE_INCENSE" => quest.Target == 1 ? "Use an Incense" : $"Use {n} Incense",
            "QUEST_LAND_THROW" => $"Make {n} {ThrowName(quest.ThrowType)}",
            _ => "Complete a special task",
        };
    }

    private static string TypeNote(GameMasterQuest quest) =>
        quest.Types.Count == 0 ? "" : $" with {string.Join(" or ", quest.Types.Select(Words))}-type Pokémon";

    private static string ThrowName(string? throwType) => throwType switch
    {
        "ACTIVITY_CATCH_EXCELLENT_THROW" => "Excellent Throws",
        "ACTIVITY_CATCH_GREAT_THROW" => "Great Throws",
        "ACTIVITY_CATCH_NICE_THROW" => "Nice Throws",
        _ => "throws",
    };

    private static string ItemName(string itemId) =>
        _itemNames.TryGetValue(itemId, out var name) ? name : Words(itemId.Replace("ITEM_", "", StringComparison.Ordinal));

    /// <summary><c>DRAGON_ASCENT</c> → "Dragon Ascent".</summary>
    internal static string Words(string id) =>
        string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
}
