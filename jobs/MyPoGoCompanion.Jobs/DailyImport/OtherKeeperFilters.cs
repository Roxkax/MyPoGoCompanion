using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Engine.Raids;
using MyPoGoCompanion.Engine.Search;

namespace MyPoGoCompanion.Jobs.DailyImport;

internal static class OtherKeeperFilters
{
    // Megas announced for Legends: Z-A and Mega Dimension, from the legacy keeper filters.
    // Released forms are picked up from PvPoke; their numbers no longer need to be added explicitly.
    private static readonly int[] _announcedMegas =
    [
        36, 154, 160, 358, 478, 485, 491, 500, 530, 545, 560, 604, 609, 623, 668, 670, 678,
        689, 691, 701, 718, 740, 768, 780, 801, 807, 952, 970, 978, 998,
    ];

    public static void Add(ImportedGameData data, IReadOnlyList<PokemonEntry> entries, RaidRanker raidRanker, IReadOnlyList<TypeRanking> rankings)
    {
        var (attackers, preEvolutions) = RaidRanker.Selection(rankings);
        var raidDexes = attackers.Concat(preEvolutions).ToHashSet();
        var releasedMegas = entries.Where(entry => entry.IsMega && entry.Released && !entry.IsShadow)
            .Select(entry => entry.Dex).ToHashSet();
        var megaDexes = releasedMegas.Concat(_announcedMegas).ToHashSet();
        var explicitMegas = _announcedMegas.Except(releasedMegas).Concat([382, 383]).ToHashSet();
        var earlierStages = entries.Where(entry => !entry.IsMega && !entry.IsShadow && megaDexes.Contains(entry.Dex))
            .SelectMany(raidRanker.PreEvolutions).Except(megaDexes).ToHashSet();

        foreach (var (variant, rule, minAttack) in new[] { ("", "3-4attack", 3), ("attack-15", "4attack", 4) })
        {
            if (raidDexes.Count != 0)
            {
                var raid = $"{KeeperStringEncoder.DexRanges(raidDexes)}&{rule}";
                AddChecked(data, KeeperFilterKind.Raid, variant, raid, null,
                    (dex, attack, _) => raidDexes.Contains(dex) && attack >= minAttack);
            }

            AddChecked(data, KeeperFilterKind.MaxBattle, variant, $"maxmove1-&{rule}", "maxmove1-",
                (_, attack, eligible) => eligible && attack >= minAttack);
        }

        var mega = $"mega0-,{KeeperStringEncoder.DexRanges(explicitMegas)}";
        AddChecked(data, KeeperFilterKind.Mega, "", mega, "mega0-",
            (dex, _, eligible) => eligible || explicitMegas.Contains(dex));
        var megaWithPre = earlierStages.Count == 0 ? mega : $"{mega},{KeeperStringEncoder.DexRanges(earlierStages)}";
        AddChecked(data, KeeperFilterKind.Mega, "with-pre-evolutions", megaWithPre, "mega0-",
            (dex, _, eligible) => eligible || explicitMegas.Contains(dex) || earlierStages.Contains(dex));
    }

    private static void AddChecked(ImportedGameData data, KeeperFilterKind kind, string variant, string text, string? flag,
        Func<int, int, bool, bool> expected)
    {
        if (text.Length > KeeperStringCheck.MaxLength)
        {
            throw new ImportException($"The {kind} keeper string exceeds {KeeperStringCheck.MaxLength} characters.");
        }

        var eligible = flag is null ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal) { flag };
        var search = GameSearch.Parse(text, eligible);
        var lastDex = Math.Max(KeeperStringCheck.LastDexChecked, data.Species.Select(species => species.Dex).DefaultIfEmpty().Max());
        for (var dex = 1; dex <= lastDex; dex++)
        {
            for (var attack = 0; attack < 5; attack++)
            {
                if (search.Matches(new SearchSubject(dex, attack, 0, 0)) != expected(dex, attack, false)
                    || search.Matches(new SearchSubject(dex, attack, 0, 0, Flags: eligible)) != expected(dex, attack, flag is not null))
                {
                    throw new ImportException($"The {kind} keeper string did not match the expected result for Pokédex #{dex}.");
                }
            }
        }

        data.KeeperFilters.Add(new KeeperFilter { Kind = kind, Variant = variant, Part = 1, SearchString = text });
    }
}
