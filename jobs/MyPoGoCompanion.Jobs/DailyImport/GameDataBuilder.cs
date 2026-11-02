using System.Globalization;
using System.Text.RegularExpressions;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Rankings;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>Game data as the sources describe it today, ready to be compared with the database.</summary>
public sealed class ImportedGameData
{
    public List<PokemonType> Types { get; } = [];

    public List<Family> Families { get; } = [];

    public List<Species> Species { get; } = [];

    public List<Form> Forms { get; } = [];

    public List<Move> Moves { get; } = [];

    public List<FormMove> FormMoves { get; } = [];

    public List<Evolution> Evolutions { get; } = [];

    public List<CpMultiplier> CpMultipliers { get; } = [];

    public List<PvpRanking> PvpRankings { get; } = [];

    /// <summary>What the import couldn't use, in plain words (kept in the run log; the run still succeeds).</summary>
    public List<string> Warnings { get; } = [];
}

/// <summary>
/// Turns the downloaded files into game data. PvPoke is the main source (species, form ids, stats, types, movepools,
/// Elite TM and Legacy flags, released, Shadow, families, nicknames, buddy distance, second-move cost, PvP move numbers
/// and rankings); the game master adds raid move numbers, evolution steps and Dynamax/Gigantamax (decisions
/// 2026-11-02). Shiny stays unknown: neither source says which shinies are out.
/// </summary>
public static partial class GameDataBuilder
{
    /// <summary>The highest level a Pokémon reaches (51 with a Best Buddy).</summary>
    public const decimal MaxLevel = 51m;

    /// <summary>
    /// The 18 types in the game's order, with the game's Portuguese names: used when the game text can't be read
    /// (the text's <c>pokemon_type_*</c> names win when it can).
    /// </summary>
    internal static readonly (string Id, string En, string Pt)[] TypeList =
    [
        ("normal", "Normal", "Normal"), ("fire", "Fire", "Fogo"), ("water", "Water", "Água"),
        ("electric", "Electric", "Elétrico"), ("grass", "Grass", "Planta"), ("ice", "Ice", "Gelo"),
        ("fighting", "Fighting", "Lutador"), ("poison", "Poison", "Venenoso"), ("ground", "Ground", "Terrestre"),
        ("flying", "Flying", "Voador"), ("psychic", "Psychic", "Psíquico"), ("bug", "Bug", "Inseto"),
        ("rock", "Rock", "Pedra"), ("ghost", "Ghost", "Fantasma"), ("dragon", "Dragon", "Dragão"),
        ("dark", "Dark", "Sombrio"), ("steel", "Steel", "Aço"), ("fairy", "Fairy", "Fada"),
    ];

    private static readonly int[] _generationStarts = [1, 152, 252, 387, 494, 650, 722, 810, 906];

    private static readonly Dictionary<string, string> _regionalTags = new(StringComparer.Ordinal)
    {
        ["alolan"] = "_ALOLA",
        ["galarian"] = "_GALARIAN",
        ["hisuian"] = "_HISUIAN",
        ["paldean"] = "_PALDEA",
    };

    /// <summary>PvPoke move ids the game master spells differently (as in the Python build's raid_rank.py).</summary>
    private static readonly Dictionary<string, string> _moveAliases = new(StringComparer.Ordinal)
    {
        ["FUTURE_SIGHT"] = "FUTURESIGHT",
        ["PYRO_BALL"] = "PYROBALL",
        ["TECHNO_BLAST_DOUSE"] = "TECHNO_BLAST_WATER",
    };

    private static readonly (League League, Func<SourceFiles, SourceFile> File)[] _leagues =
    [
        (League.Great, files => files.RankingsGreat),
        (League.Ultra, files => files.RankingsUltra),
        (League.Master, files => files.RankingsMaster),
    ];

    public static ImportedGameData Build(SourceFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var pvpoke = PvpokeSource.ReadGameMaster(files.PvpokeGameMaster);
        var gameMaster = GameMasterSource.Read(files.GameMaster);
        var multipliers = PvpokeSource.ReadCpMultipliers(files.CpMultipliers, MaxLevel);
        var rankings = _leagues.Select(league => (league.League, Rows: PvpokeSource.ReadRankings(league.File(files)))).ToList();

        // The Portuguese names are optional (decision 2026-11-02): without the text the names stay null here, and
        // the save keeps the ones the database already has.
        var problems = files.GameTextProblems.ToList();
        GameText? text = null;
        if (files.GameText is not null)
        {
            try
            {
                text = GameText.Read(files.GameText, files.GameTextRemote);
            }
            catch (ImportException exception)
            {
                problems.Add(exception.Message);
            }
        }

        var data = Build(pvpoke, gameMaster, multipliers, rankings, text);
        foreach (var problem in problems)
        {
            data.Warnings.Add(text is null
                ? $"Portuguese names not updated, the last ones are kept: {problem}"
                : $"Portuguese names from the game release only (the server's newer texts are missing): {problem}");
        }

        return data;
    }

    public static ImportedGameData Build(
        PvpokeGameMaster pvpoke,
        GameMasterSource gameMaster,
        IReadOnlyList<(decimal Level, double Value)> multipliers,
        IReadOnlyList<(League League, List<PvpokeRanking> Rows)> rankings,
        GameText? text = null)
    {
        ArgumentNullException.ThrowIfNull(pvpoke);
        ArgumentNullException.ThrowIfNull(gameMaster);
        ArgumentNullException.ThrowIfNull(multipliers);
        ArgumentNullException.ThrowIfNull(rankings);

        var data = new ImportedGameData();
        var typeIds = TypeList.Select(type => type.Id).ToHashSet(StringComparer.Ordinal);
        data.Types.AddRange(TypeList.Select((type, index) => new PokemonType
        {
            Id = type.Id,
            NameEn = type.En,
            NamePt = text?.Get("pokemon_type_" + type.Id) ?? type.Pt,
            SortOrder = index + 1,
        }));
        data.CpMultipliers.AddRange(multipliers.Select(m => new CpMultiplier { Level = m.Level, Value = m.Value }));
        var cpm50 = multipliers.First(m => m.Level == 50m).Value;

        AddMoves(data, pvpoke, gameMaster, typeIds);
        var moveIds = data.Moves.Select(move => move.Id).ToHashSet(StringComparer.Ordinal);

        // Shadow versions are PvPoke entries of their own ("_shadow"); here they are a flag on the form.
        var shadowReleased = pvpoke.Pokemon
            .Where(p => p.SpeciesId.EndsWith("_shadow", StringComparison.Ordinal) && p.Released)
            .Select(p => p.SpeciesId[..^"_shadow".Length])
            .ToHashSet(StringComparer.Ordinal);
        var entries = pvpoke.Pokemon
            .Where(p => !p.SpeciesId.EndsWith("_shadow", StringComparison.Ordinal) && !p.HasTag("duplicate") && p.Dex > 0)
            .GroupBy(p => p.SpeciesId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var unknownTypes = entries.Where(p => !KnownTypes(p, typeIds)).Select(p => p.SpeciesId).ToList();
        Warn(data, unknownTypes, "forms with a type the import doesn't know, left out");
        entries = entries.Where(p => KnownTypes(p, typeIds)).ToList();

        var formIdByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var matches = new Dictionary<string, GameMasterPokemon>(StringComparer.Ordinal);
        var unmatched = new List<string>();
        foreach (var entry in entries.Where(p => !p.HasTag("mega")))
        {
            if (Match(entry, gameMaster) is { } match)
            {
                matches[entry.SpeciesId] = match;
                formIdByKey.TryAdd(match.Key, entry.SpeciesId);
                formIdByKey.TryAdd(WithoutNormal(match.Key), entry.SpeciesId);
            }
            else
            {
                unmatched.Add(entry.SpeciesId);
            }
        }

        Warn(data, unmatched, "forms not found in the game master (no evolutions or Dynamax for them yet)");

        var families = new SortedSet<string>(StringComparer.Ordinal);
        var defaultByDex = new Dictionary<int, string>();
        foreach (var dexGroup in entries.GroupBy(p => p.Dex).OrderBy(group => group.Key))
        {
            var main = MainForm(dexGroup.ToList(), gameMaster);
            defaultByDex[dexGroup.Key] = main.SpeciesId;
            var family = dexGroup.Select(p => p.Family?.Id).FirstOrDefault(id => !string.IsNullOrEmpty(id))
                ?? (matches.TryGetValue(main.SpeciesId, out var gm) ? gm.FamilyId : null)
                ?? "FAMILY_" + main.SpeciesId.ToUpperInvariant();
            families.Add(family);
            data.Species.Add(new Species
            {
                Dex = dexGroup.Key,
                NameEn = SpeciesName(main.SpeciesName),
                Generation = _generationStarts.Count(start => dexGroup.Key >= start),
                Category = main.HasTag("mythical") ? SpeciesCategory.Mythical
                    : main.HasTag("ultrabeast") ? SpeciesCategory.UltraBeast
                    : main.HasTag("legendary") ? SpeciesCategory.Legendary
                    : SpeciesCategory.None,
                FamilyId = family,
                Nicknames = [.. main.Nicknames],
            });

            foreach (var entry in dexGroup)
            {
                data.Forms.Add(BuildForm(entry, main.SpeciesId, matches, gameMaster, shadowReleased, cpm50));
                AddMovepool(data, entry, moveIds);
            }
        }

        data.Families.AddRange(families.Select(id => new Family { Id = id }));
        AddEvolutions(data, matches, gameMaster, formIdByKey, defaultByDex);
        AddRankings(data, rankings, moveIds);
        if (text is not null)
        {
            PortugueseNames.Apply(data, text, gameMaster);
        }

        return data;
    }

    /// <summary>
    /// The form the Pokédex line shows: the one with the stats (and types) of the game master's template without a
    /// form (Zacian's Hero, not its Crowned Sword; Darmanitan's Standard, not its Galarian Zen, although PvPoke lists
    /// those first), else PvPoke's first. Never a Mega.
    /// </summary>
    internal static PvpokePokemon MainForm(List<PvpokePokemon> forms, GameMasterSource gameMaster)
    {
        var candidates = forms.Where(p => !p.HasTag("mega")).ToList();
        if (candidates.Count == 0)
        {
            return forms[0];
        }

        var plain = gameMaster.PokemonByDex.GetValueOrDefault(forms[0].Dex, []).FirstOrDefault(t => t.Form is null);
        if (plain is null)
        {
            return candidates[0];
        }

        // The template without a form can carry another form's types (Zacian's has the Hero's stats and the Crowned
        // Sword's types), so the stats alone decide when nothing matches both; a regional form only as a last resort.
        return candidates.FirstOrDefault(p => SameStatsAndTypes(p, plain))
            ?? candidates.Where(p => SameStats(p, plain)).OrderBy(p => p.Tags.Any(_regionalTags.ContainsKey)).FirstOrDefault()
            ?? candidates[0];
    }

    private static Form BuildForm(
        PvpokePokemon entry,
        string defaultId,
        Dictionary<string, GameMasterPokemon> matches,
        GameMasterSource gameMaster,
        HashSet<string> shadowReleased,
        double cpm50)
    {
        var isMega = entry.HasTag("mega");
        var kind = isMega ? (entry.SpeciesId.EndsWith("_primal", StringComparison.Ordinal) ? FormKind.Primal : FormKind.Mega)
            : entry.Tags.Any(_regionalTags.ContainsKey) ? FormKind.Regional
            : entry.SpeciesId == defaultId ? FormKind.Base
            : FormKind.Alternate;
        var key = matches.TryGetValue(entry.SpeciesId, out var match) ? match.Key : null;
        var types = entry.Types.Where(type => type != "none").ToList();
        return new Form
        {
            Id = entry.SpeciesId,
            Dex = entry.Dex,
            Kind = kind,
            IsDefault = entry.SpeciesId == defaultId,
            GameMasterId = key,
            NameEn = entry.SpeciesName,
            Type1Id = types[0],
            Type2Id = types.Count > 1 && types[1] != types[0] ? types[1] : null,
            Attack = entry.BaseStats.Atk,
            Defense = entry.BaseStats.Def,
            Stamina = entry.BaseStats.Hp,
            MaxCp50 = Cp(entry.BaseStats, cpm50),
            BaseFormId = isMega && entry.SpeciesId != defaultId ? defaultId : null,
            Released = entry.Released,
            ShadowReleased = shadowReleased.Contains(entry.SpeciesId),
            ShinyReleased = null,
            CanDynamax = key is not null && (HasLook(gameMaster.CanDynamax, key) || HasLook(gameMaster.CanGigantamax, key)),
            CanGigantamax = key is not null && HasLook(gameMaster.CanGigantamax, key),
            BuddyDistanceKm = entry.BuddyDistance,
            ThirdMoveStardust = entry.ThirdMoveCost,
        };
    }

    private static void AddMoves(ImportedGameData data, PvpokeGameMaster pvpoke, GameMasterSource gameMaster, HashSet<string> typeIds)
    {
        var noRaidNumbers = new List<string>();
        var unknownType = new List<string>();
        foreach (var move in pvpoke.Moves.GroupBy(m => m.MoveId, StringComparer.Ordinal).Select(group => group.First()))
        {
            if (!typeIds.Contains(move.Type))
            {
                unknownType.Add(move.MoveId);
                continue;
            }

            var fast = move.IsFast;
            var raid = RaidNumbers(move.MoveId, fast, gameMaster);
            if (raid is null && !NoRaidVersion().IsMatch(move.MoveId))
            {
                noRaidNumbers.Add(move.MoveId);
            }

            var self = move.BuffsSelf ?? (move.BuffTarget == "self" ? move.Buffs : null) ?? [0, 0];
            var opponent = move.BuffsOpponent ?? (move.BuffTarget == "opponent" ? move.Buffs : null) ?? [0, 0];
            data.Moves.Add(new Move
            {
                Id = move.MoveId,
                Kind = fast ? MoveKind.Fast : MoveKind.Charged,
                TypeId = move.Type,
                NameEn = move.Name,
                RaidPower = raid?.Power ?? 0,
                RaidDurationMs = raid?.DurationMs ?? 0,
                RaidDamageWindowStartMs = raid?.DamageWindowStartMs ?? 0,
                RaidDamageWindowEndMs = raid?.DamageWindowEndMs ?? 0,
                RaidEnergyDelta = raid?.EnergyDelta ?? 0,
                PvpPower = move.Power,
                PvpEnergyDelta = fast ? move.EnergyGain : -move.Energy,
                PvpTurns = fast ? Math.Max(1, move.Turns) : null,
                PvpBuffChance = move.BuffApplyChance is { } chance
                    && double.TryParse(chance, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 0, 1) : null,
                PvpSelfAttackStages = Stage(self, 0),
                PvpSelfDefenseStages = Stage(self, 1),
                PvpOpponentAttackStages = Stage(opponent, 0),
                PvpOpponentDefenseStages = Stage(opponent, 1),
            });
        }

        Warn(data, unknownType, "moves with a type the import doesn't know, left out");
        Warn(data, noRaidNumbers, "moves without raid numbers in the game master (raid numbers 0)");
    }

    /// <summary>The game master's raid numbers for a PvPoke move, or null (Hidden Power and Aegislash's charge moves have none).</summary>
    internal static GameMasterMove? RaidNumbers(string moveId, bool fast, GameMasterSource gameMaster)
    {
        if (NoRaidVersion().IsMatch(moveId))
        {
            return null;
        }

        return gameMaster.Moves.GetValueOrDefault(GameMasterMoveKey(moveId, fast));
    }

    /// <summary>The game master's name for a PvPoke move (<c>FIRE_SPIN</c> fast → <c>FIRE_SPIN_FAST</c>).</summary>
    internal static string GameMasterMoveKey(string moveId, bool fast)
    {
        var key = _moveAliases.GetValueOrDefault(moveId, moveId);
        return fast ? key + "_FAST" : key;
    }

    private static void AddMovepool(ImportedGameData data, PvpokePokemon entry, HashSet<string> moveIds)
    {
        var elite = entry.EliteMoves.ToHashSet(StringComparer.Ordinal);
        var legacy = entry.LegacyMoves.ToHashSet(StringComparer.Ordinal);
        var all = entry.FastMoves.Concat(entry.ChargedMoves).Concat(entry.ExtraChargedMoves).Concat(entry.EliteMoves).Concat(entry.LegacyMoves);
        var missing = new List<string>();
        foreach (var moveId in all.Distinct(StringComparer.Ordinal))
        {
            if (!moveIds.Contains(moveId))
            {
                missing.Add(moveId);
                continue;
            }

            data.FormMoves.Add(new FormMove
            {
                FormId = entry.SpeciesId,
                MoveId = moveId,
                Availability = legacy.Contains(moveId) ? MoveAvailability.Legacy
                    : elite.Contains(moveId) ? MoveAvailability.EliteTm
                    : MoveAvailability.Normal,
            });
        }

        if (missing.Count > 0)
        {
            data.Warnings.Add($"{entry.SpeciesId}: moves not in PvPoke's move list, left out: {string.Join(", ", missing)}");
        }
    }

    private static void AddEvolutions(
        ImportedGameData data,
        Dictionary<string, GameMasterPokemon> matches,
        GameMasterSource gameMaster,
        Dictionary<string, string> formIdByKey,
        Dictionary<int, string> defaultByDex)
    {
        var steps = new Dictionary<(string From, string To), (Evolution Step, bool IsEvent)>();
        foreach (var (fromId, template) in matches)
        {
            foreach (var branch in template.Evolutions)
            {
                var toId = Resolve(branch.Target, gameMaster, formIdByKey, defaultByDex);
                if (toId is null || toId == fromId)
                {
                    continue;
                }

                var quest = branch.QuestId is null ? null : gameMaster.Quests.GetValueOrDefault(branch.QuestId);
                var step = new Evolution
                {
                    FromFormId = fromId,
                    ToFormId = toId,
                    Candy = branch.Candy,
                    CandyAfterTrade = branch.NoCandyAfterTrade ? 0 : null,
                    ItemId = branch.ItemId,
                    BuddyDistanceKm = branch.BuddyDistanceKm,
                    TimeOfDay = branch.TimeOfDay,
                    LureId = branch.LureId,
                    Gender = branch.Gender is "male" or "female" ? branch.Gender : null,
                    TaskEn = EvolutionTask.Describe(branch, quest),
                };

                // Community Day and event versions of a step sit next to the usual one; the usual one is kept.
                var isEvent = branch.QuestId is { } id && (id.Contains("_CD_", StringComparison.Ordinal) || id.Contains("_EVENT_", StringComparison.Ordinal));
                if (!steps.TryGetValue((fromId, toId), out var existing) || (existing.IsEvent && !isEvent))
                {
                    steps[(fromId, toId)] = (step, isEvent);
                }
            }
        }

        data.Evolutions.AddRange(steps.OrderBy(step => step.Key.From, StringComparer.Ordinal)
            .ThenBy(step => step.Key.To, StringComparer.Ordinal).Select(step => step.Value.Step));
    }

    private static string? Resolve(
        string target, GameMasterSource gameMaster, Dictionary<string, string> formIdByKey, Dictionary<int, string> defaultByDex)
    {
        if (formIdByKey.TryGetValue(target, out var id) || formIdByKey.TryGetValue(WithoutNormal(target), out id))
        {
            return id;
        }

        // A form the import doesn't have (a costume): evolve into the species' usual form.
        var dex = gameMaster.Pokemon.GetValueOrDefault(target)?.Dex;
        return dex is { } d && defaultByDex.TryGetValue(d, out var main) ? main : null;
    }

    private static void AddRankings(ImportedGameData data, IReadOnlyList<(League League, List<PvpokeRanking> Rows)> rankings, HashSet<string> moveIds)
    {
        var formIds = data.Forms.Select(form => form.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (league, rows) in rankings)
        {
            var seen = new HashSet<(string, bool)>();
            var skipped = new List<string>();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                // PvPoke pads a moveset without a second charged move with "none" (Unown).
                var moveset = row.Moveset.Where(move => move is { Length: > 0 } and not "none").ToList();
                var shadow = row.SpeciesId.EndsWith("_shadow", StringComparison.Ordinal);
                var formId = shadow ? row.SpeciesId[..^"_shadow".Length] : row.SpeciesId;
                if (!formIds.Contains(formId) || moveset.Count < 2 || !moveset.All(moveIds.Contains))
                {
                    skipped.Add(row.SpeciesId);
                    continue;
                }

                if (seen.Add((formId, shadow)))
                {
                    data.PvpRankings.Add(new PvpRanking
                    {
                        League = league,
                        FormId = formId,
                        IsShadow = shadow,
                        Rank = index + 1,
                        Score = row.Score,
                        FastMoveId = moveset[0],
                        ChargedMove1Id = moveset[1],
                        ChargedMove2Id = moveset.Count > 2 ? moveset[2] : null,
                    });
                }
            }

            Warn(data, skipped, $"{league} League ranking rows for unknown forms or moves, left out");
        }
    }

    /// <summary>
    /// The game master template for a PvPoke form: by name (<c>vulpix_alolan</c> → <c>VULPIX_ALOLA</c>, or with
    /// <c>_NORMAL</c>), else the one template of that Pokédex number with the same stats and types (preferring
    /// <c>_NORMAL</c>, then a template without a form).
    /// </summary>
    internal static GameMasterPokemon? Match(PvpokePokemon entry, GameMasterSource gameMaster)
    {
        var name = entry.SpeciesId.ToUpperInvariant();
        foreach (var (tag, suffix) in _regionalTags)
        {
            name = name.Replace("_" + tag.ToUpperInvariant(), suffix, StringComparison.Ordinal);
        }

        foreach (var candidate in new[] { name, name + "_NORMAL" })
        {
            if (gameMaster.Pokemon.TryGetValue(candidate, out var byName) && byName.Dex == entry.Dex)
            {
                return byName;
            }
        }

        var same = gameMaster.PokemonByDex.GetValueOrDefault(entry.Dex, []).Where(t => SameStatsAndTypes(entry, t)).ToList();
        return same.FirstOrDefault(t => t.Key.EndsWith("_NORMAL", StringComparison.Ordinal))
            ?? same.FirstOrDefault(t => t.Form is null)
            ?? same.FirstOrDefault();
    }

    private static bool SameStats(PvpokePokemon entry, GameMasterPokemon template) =>
        template.Attack == entry.BaseStats.Atk && template.Defense == entry.BaseStats.Def && template.Stamina == entry.BaseStats.Hp;

    private static bool SameStatsAndTypes(PvpokePokemon entry, GameMasterPokemon template) =>
        SameStats(entry, template)
        && template.Types.Order(StringComparer.Ordinal)
            .SequenceEqual(entry.Types.Where(type => type != "none").Order(StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>15/15/15 at level 50 (the game's CP formula, at least 10).</summary>
    internal static int Cp(PvpokeStats stats, double cpm) =>
        Math.Max(10, (int)Math.Floor((stats.Atk + 15) * Math.Sqrt(stats.Def + 15) * Math.Sqrt(stats.Hp + 15) * cpm * cpm / 10));

    /// <summary>"Giratina (Altered)" → "Giratina".</summary>
    internal static string SpeciesName(string name) => TrailingBrackets().Replace(name, "");

    private static bool HasLook(HashSet<string> looks, string key) =>
        looks.Contains(key) || looks.Contains(WithoutNormal(key)) || looks.Contains(key + "_NORMAL");

    private static string WithoutNormal(string key) =>
        key.EndsWith("_NORMAL", StringComparison.Ordinal) ? key[..^"_NORMAL".Length] : key;

    private static bool KnownTypes(PvpokePokemon entry, HashSet<string> typeIds)
    {
        var types = entry.Types.Where(type => type != "none").ToList();
        return types.Count > 0 && types.All(typeIds.Contains);
    }

    private static int Stage(List<int> stages, int index) => stages.Count > index ? stages[index] : 0;

    private static void Warn(ImportedGameData data, List<string> ids, string what)
    {
        if (ids.Count > 0)
        {
            var shown = ids.Count > 20 ? string.Join(", ", ids.Take(20)) + $" and {ids.Count - 20} more" : string.Join(", ", ids);
            data.Warnings.Add($"{ids.Count} {what}: {shown}");
        }
    }

    [GeneratedRegex(@"^(HIDDEN_POWER|AEGISLASH_CHARGE)")]
    private static partial Regex NoRaidVersion();

    [GeneratedRegex(@"\s*\([^)]*\)$")]
    private static partial Regex TrailingBrackets();
}
