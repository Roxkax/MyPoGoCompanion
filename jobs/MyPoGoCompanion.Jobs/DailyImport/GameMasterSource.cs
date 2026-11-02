using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>
/// The parts of the datamined game master the import reads: Pokémon templates (stats and types, to match PvPoke's
/// forms; families and evolution steps), which forms can Dynamax or Gigantamax, raid (PvE) move numbers and the
/// evolution tasks. Templates are keyed by their form name (<c>VULPIX_ALOLA</c>), or the Pokémon's name for a template
/// without a form (<c>VULPIX</c>).
/// </summary>
public sealed partial class GameMasterSource
{
    public Dictionary<string, GameMasterPokemon> Pokemon { get; } = new(StringComparer.Ordinal);

    public Dictionary<int, List<GameMasterPokemon>> PokemonByDex { get; } = [];

    /// <summary>Forms with a Max Battle look (<c>BREAD_MODE</c>): what the game's code can Dynamax.</summary>
    public HashSet<string> CanDynamax { get; } = new(StringComparer.Ordinal);

    /// <summary>Forms with a Gigantamax look (<c>BREAD_DOUGH_MODE</c>).</summary>
    public HashSet<string> CanGigantamax { get; } = new(StringComparer.Ordinal);

    /// <summary>Raid numbers per move, keyed by the template name after <c>V0xxx_MOVE_</c> (fast moves end in <c>_FAST</c>).</summary>
    public Dictionary<string, GameMasterMove> Moves { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, GameMasterQuest> Quests { get; } = new(StringComparer.Ordinal);

    public static GameMasterSource Read(SourceFile file)
    {
        var source = new GameMasterSource();
        try
        {
            using var document = JsonDocument.Parse(file.Content);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new ImportException($"The {file.Name} isn't a list of templates: its format may have changed.");
            }

            foreach (var template in document.RootElement.EnumerateArray())
            {
                source.Add(template);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new ImportException($"The {file.Name} isn't the JSON the import expects ({exception.Message}).", exception);
        }

        if (source.Pokemon.Count == 0 || source.Moves.Count == 0)
        {
            throw new ImportException($"The {file.Name} has no Pokémon or no move templates: its format may have changed.");
        }

        return source;
    }

    private void Add(JsonElement template)
    {
        if (!template.TryGetProperty("templateId", out var idElement) || !template.TryGetProperty("data", out var data))
        {
            return;
        }

        var templateId = idElement.GetString() ?? "";
        if (data.TryGetProperty("pokemonSettings", out var pokemon) && PokemonTemplate().Match(templateId) is { Success: true } p)
        {
            AddPokemon(int.Parse(p.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), pokemon);
        }
        else if (data.TryGetProperty("moveSettings", out var move) && MoveTemplate().Match(templateId) is { Success: true } m)
        {
            Moves[m.Groups[1].Value] = new GameMasterMove(
                Number(move, "power"),
                (int)Number(move, "durationMs"),
                (int)Number(move, "damageWindowStartMs"),
                (int)Number(move, "damageWindowEndMs"),
                (int)Number(move, "energyDelta"));
        }
        else if (data.TryGetProperty("pokemonExtendedSettings", out var extended))
        {
            AddMaxLooks(extended);
        }
        else if (data.TryGetProperty("evolutionQuestTemplate", out var quest))
        {
            AddQuest(quest);
        }
    }

    private void AddPokemon(int dex, JsonElement settings)
    {
        var pokemonId = Text(settings, "pokemonId") ?? "";
        var form = Text(settings, "form");
        var stats = settings.TryGetProperty("stats", out var s) ? s : default;
        var types = new[] { Text(settings, "type"), Text(settings, "type2") }
            .Where(type => type is not null)
            .Select(type => type!.Replace("POKEMON_TYPE_", "", StringComparison.Ordinal).ToLowerInvariant())
            .ToList();
        var branches = new List<GameMasterEvolution>();
        if (settings.TryGetProperty("evolutionBranch", out var evolutionBranch))
        {
            foreach (var branch in evolutionBranch.EnumerateArray())
            {
                // Mega Evolutions are temporary and are forms of their own (PvPoke's _mega ids), not evolution steps.
                if (Text(branch, "evolution") is { } target && !branch.TryGetProperty("temporaryEvolution", out _))
                {
                    branches.Add(ReadBranch(target, branch));
                }
            }
        }

        var entry = new GameMasterPokemon(
            form ?? pokemonId,
            dex,
            pokemonId,
            form,
            stats.ValueKind == JsonValueKind.Object ? (int)Number(stats, "baseAttack") : 0,
            stats.ValueKind == JsonValueKind.Object ? (int)Number(stats, "baseDefense") : 0,
            stats.ValueKind == JsonValueKind.Object ? (int)Number(stats, "baseStamina") : 0,
            types,
            Text(settings, "familyId"),
            branches);

        // The first template of a name wins (the game master lists each once; a repeat would be a datamine artefact).
        if (Pokemon.TryAdd(entry.Key, entry))
        {
            if (!PokemonByDex.TryGetValue(dex, out var list))
            {
                PokemonByDex[dex] = list = [];
            }

            list.Add(entry);
        }
    }

    private static GameMasterEvolution ReadBranch(string target, JsonElement branch)
    {
        string? quest = null;
        if (branch.TryGetProperty("questDisplay", out var display) && display.ValueKind == JsonValueKind.Array)
        {
            quest = display.EnumerateArray().Select(q => Text(q, "questRequirementTemplateId")).FirstOrDefault(q => q is not null);
        }

        return new GameMasterEvolution(
            Text(branch, "form") ?? target,
            (int)Number(branch, "candyCost"),
            Flag(branch, "noCandyCostViaTrade"),
            Text(branch, "evolutionItemRequirement"),
            branch.TryGetProperty("evolutionItemRequirementCost", out var cost) ? cost.GetInt32() : null,
            branch.TryGetProperty("kmBuddyDistanceRequirement", out var km) ? (int)Math.Round(km.GetDouble()) : null,
            Flag(branch, "mustBeBuddy"),
            Flag(branch, "onlyDaytime") ? "day" : Flag(branch, "onlyNighttime") ? "night" : null,
            Text(branch, "lureItemRequirement"),
            Text(branch, "genderRequirement")?.ToLowerInvariant(),
            quest,
            Flag(branch, "onlyFullMoon"),
            Flag(branch, "onlyUpsideDown"),
            Flag(branch, "onlyDuskPeriod"),
            Text(branch, "evolutionMoveRequirement"));
    }

    private void AddMaxLooks(JsonElement extended)
    {
        if (!extended.TryGetProperty("breadOverrides", out var overrides) || overrides.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var key = Text(extended, "form") ?? Text(extended, "uniqueId");
        if (key is null)
        {
            return;
        }

        foreach (var look in overrides.EnumerateArray())
        {
            switch (Text(look, "breadMode"))
            {
                case "BREAD_MODE":
                    CanDynamax.Add(key);
                    break;
                case "BREAD_DOUGH_MODE":
                    CanGigantamax.Add(key);
                    break;
            }
        }
    }

    private void AddQuest(JsonElement quest)
    {
        var id = Text(quest, "questTemplateId");
        if (id is null)
        {
            return;
        }

        var target = 0;
        var types = new List<string>();
        var conditions = new List<string>();
        string? throwType = null;
        if (quest.TryGetProperty("goals", out var goals) && goals.ValueKind == JsonValueKind.Array)
        {
            foreach (var goal in goals.EnumerateArray())
            {
                target = Math.Max(target, (int)Number(goal, "target"));
                if (!goal.TryGetProperty("condition", out var list))
                {
                    continue;
                }

                foreach (var condition in list.EnumerateArray())
                {
                    conditions.Add(Text(condition, "type") ?? "");
                    foreach (var property in condition.EnumerateObject().Where(property => property.Value.ValueKind == JsonValueKind.Object))
                    {
                        types.AddRange(TypeNames(property.Value, "pokemonType"));
                        types.AddRange(TypeNames(property.Value, "opponentPokemonType"));
                        throwType ??= Text(property.Value, "throwType");
                        if (property.Value.TryGetProperty("combatType", out var combat) && combat.ValueKind == JsonValueKind.Array)
                        {
                            conditions.AddRange(combat.EnumerateArray().Select(type => type.GetString() ?? ""));
                        }
                    }
                }
            }
        }

        Quests[id] = new GameMasterQuest(id, Text(quest, "questType") ?? "", target, types, conditions, throwType);
    }

    private static IEnumerable<string> TypeNames(JsonElement element, string property) =>
        element.TryGetProperty(property, out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(type => (type.GetString() ?? "").Replace("POKEMON_TYPE_", "", StringComparison.Ordinal).ToLowerInvariant())
            : [];

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;

    private static bool Flag(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    [GeneratedRegex(@"^V(\d{4})_POKEMON_")]
    private static partial Regex PokemonTemplate();

    [GeneratedRegex(@"^V\d{4}_MOVE_(.+)$")]
    private static partial Regex MoveTemplate();
}

public sealed record GameMasterPokemon(
    string Key,
    int Dex,
    string PokemonId,
    string? Form,
    int Attack,
    int Defense,
    int Stamina,
    IReadOnlyList<string> Types,
    string? FamilyId,
    IReadOnlyList<GameMasterEvolution> Evolutions);

/// <param name="Target">The form it evolves into (<c>NINETALES_ALOLA</c>), or the Pokémon when the branch names no form.</param>
public sealed record GameMasterEvolution(
    string Target,
    int Candy,
    bool NoCandyAfterTrade,
    string? ItemId,
    int? ItemCount,
    int? BuddyDistanceKm,
    bool MustBeBuddy,
    string? TimeOfDay,
    string? LureId,
    string? Gender,
    string? QuestId,
    bool OnlyFullMoon,
    bool OnlyUpsideDown,
    bool OnlyDusk,
    string? MoveRequired);

public sealed record GameMasterMove(double Power, int DurationMs, int DamageWindowStartMs, int DamageWindowEndMs, int EnergyDelta);

public sealed record GameMasterQuest(
    string Id, string QuestType, int Target, IReadOnlyList<string> Types, IReadOnlyList<string> Conditions, string? ThrowType);
