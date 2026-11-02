using System.Globalization;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Engine.Pvp;
using MyPoGoCompanion.Engine.Search;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>One sanity check in the run log: its name, whether it passed and, if not, what is wrong in plain words.</summary>
public sealed record ImportCheck(string Name, bool Passed, List<string> Problems, string? Detail = null);

/// <summary>
/// The sanity checks the daily import runs on the new data before publishing it (E02-F04, docs/DAILY-IMPORT.md):
/// broken data (gaps in the Pokédex, broken ranks, impossible numbers, search strings the simulator rejects) can't
/// be published, so a failed check fails the run and yesterday's data stays live. Missing data (rows the sources no
/// longer list) is not a check: it holds the run for the super admin (<see cref="Removals"/>).
/// </summary>
public static class ImportChecks
{
    /// <summary>The highest and lowest believable max CP at level 50 (today: Magikarp 310, Eternamax Eternatus 9,255).</summary>
    public const int LowestMaxCp = 10;

    public const int HighestMaxCp = 10_000;

    /// <summary>Problems listed per check before the rest are only counted.</summary>
    public const int ProblemsShown = 10;

    private static readonly (League League, int Cap, string Name)[] _leagues =
        [(League.Great, Leagues.Great, "Great League"), (League.Ultra, Leagues.Ultra, "Ultra League"), (League.Master, Leagues.Master, "Master League")];

    /// <param name="data">What the import is about to publish.</param>
    /// <param name="storedHighestDex">The highest Pokédex number already stored (0 when none).</param>
    public static List<ImportCheck> Run(ImportedGameData data, int storedHighestDex, DailyImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);
        return
        [
            Pokedex(data, storedHighestDex, options.FullPokedex),
            Numbers(data),
            PvpRankings(data),
            BestIvs(data),
            KeeperBoxes(data),
            RaidRankings(data, options.MinimumRaidRankingsPerType),
            RaidMovesets(data),
            KeeperStrings(data),
        ];
    }

    /// <summary>The run log's error for failed checks.</summary>
    public static string Explain(IEnumerable<ImportCheck> checks)
    {
        var failed = checks.Where(check => !check.Passed).ToList();
        var lines = failed.Select(check => $"{check.Name}: {string.Join(" ", check.Problems)}");
        return $"The new data failed {failed.Count} sanity check{(failed.Count == 1 ? "" : "s")}, so nothing was published "
            + $"(yesterday's data stays live). {string.Join(" ", lines)}";
    }

    private static ImportCheck Pokedex(ImportedGameData data, int storedHighestDex, bool fullPokedex)
    {
        var problems = new Problems();
        var dexes = data.Species.Select(species => species.Dex).ToHashSet();
        var highest = dexes.Count == 0 ? 0 : dexes.Max();
        if (highest == 0)
        {
            problems.Add("The sources list no Pokémon.");
        }

        if (highest < storedHighestDex)
        {
            problems.Add(F($"The highest Pokédex number is #{highest}, lower than the #{storedHighestDex} already stored."));
        }

        if (fullPokedex)
        {
            var missing = Enumerable.Range(1, highest).Where(dex => !dexes.Contains(dex)).ToList();
            if (missing.Count > 0)
            {
                problems.Add(F($"{missing.Count} Pokédex number{Plural(missing.Count)} missing between #1 and #{highest}: {Numbers(missing)}."));
            }
        }

        var mainForms = data.Forms.Where(form => form.IsDefault).GroupBy(form => form.Dex).ToDictionary(group => group.Key, group => group.Count());
        foreach (var species in data.Species.OrderBy(species => species.Dex))
        {
            var count = mainForms.GetValueOrDefault(species.Dex);
            if (count != 1)
            {
                problems.Add(F($"{species.NameEn} (#{species.Dex}) has {count} main forms (exactly 1 expected)."));
            }
        }

        return problems.Check("Pokédex", F($"{dexes.Count:N0} Pokédex numbers up to #{highest}"));
    }

    private static ImportCheck Numbers(ImportedGameData data)
    {
        var problems = new Problems();
        foreach (var form in data.Forms)
        {
            if (form.Attack <= 0 || form.Defense <= 0 || form.Stamina <= 0)
            {
                problems.Add(F($"{form.NameEn} ({form.Id}) has base stats {form.Attack}/{form.Defense}/{form.Stamina} (all above 0 expected)."));
            }
            else if (form.MaxCp50 is < LowestMaxCp or > HighestMaxCp)
            {
                problems.Add(F($"{form.NameEn} ({form.Id}) has a max CP of {form.MaxCp50:N0} at level 50 ({LowestMaxCp}–{HighestMaxCp:N0} expected)."));
            }
        }

        var levels = data.CpMultipliers.OrderBy(multiplier => multiplier.Level).ToList();
        var expected = Enumerable.Range(2, 101).Select(half => half / 2m).ToList(); // 1, 1.5 … 51
        if (!levels.Select(multiplier => multiplier.Level).SequenceEqual(expected))
        {
            problems.Add(F($"The CP multipliers cover {levels.Count} levels; every level from 1 to 51 in steps of 0.5 (101) is expected."));
        }

        for (var i = 1; i < levels.Count; i++)
        {
            if (!(levels[i].Value > levels[i - 1].Value))
            {
                problems.Add(F($"The CP multiplier of level {levels[i].Level:0.#} ({levels[i].Value}) isn't higher than level {levels[i - 1].Level:0.#}'s ({levels[i - 1].Value})."));
            }
        }

        return problems.Check("Numbers", F($"{data.Forms.Count:N0} forms, {levels.Count} CP multipliers"));
    }

    private static ImportCheck PvpRankings(ImportedGameData data)
    {
        var problems = new Problems();
        foreach (var (league, _, name) in _leagues)
        {
            var ranks = data.PvpRankings.Where(ranking => ranking.League == league).Select(ranking => ranking.Rank).ToList();
            if (ranks.Count == 0)
            {
                problems.Add($"{name} has no ranking.");
                continue;
            }

            // PvPoke's order: a form the import doesn't have leaves a gap (a warning), but no rank may repeat.
            var repeated = ranks.GroupBy(rank => rank).Where(group => group.Count() > 1 || group.Key < 1).Select(group => group.Key).Order().ToList();
            if (repeated.Count > 0)
            {
                problems.Add(F($"{name}: rank {repeated[0]} is used {ranks.Count(rank => rank == repeated[0])} times (each rank once, from 1, expected)."));
            }
        }

        return problems.Check("PvP rankings", F($"{data.PvpRankings.Count:N0} rows"));
    }

    private static ImportCheck BestIvs(ImportedGameData data)
    {
        var problems = new Problems();
        var formsPerLeague = _leagues.ToDictionary(
            league => league.League,
            league => data.PvpBestIvs.Where(best => best.League == league.League).Select(best => best.FormId).ToHashSet(StringComparer.Ordinal));
        foreach (var best in data.PvpBestIvs)
        {
            var (_, cap, name) = _leagues.Single(league => league.League == best.League);
            if (best.Cp > cap || best.Cp < 10 || best.Level is < 1 or > 51
                || best.AttackIv is < 0 or > 15 || best.DefenseIv is < 0 or > 15 || best.StaminaIv is < 0 or > 15)
            {
                problems.Add(F($"{name}: {best.FormId}'s best IVs {best.AttackIv}/{best.DefenseIv}/{best.StaminaIv} at level {best.Level:0.#} give CP {best.Cp:N0} (at most {cap:N0}, level 1–51)."));
            }
        }

        // Every Pokédex line (its main form) has best IVs in all three leagues, as the screens expect.
        foreach (var form in data.Forms.Where(form => form.IsDefault && form.Dex <= KeeperCalculator.MaxDex
                     && form.Kind != FormKind.Mega && !PokemonEntry.BattleOnly.Contains(form.Id)).OrderBy(form => form.Dex))
        {
            foreach (var (league, _, name) in _leagues)
            {
                if (!formsPerLeague[league].Contains(form.Id))
                {
                    problems.Add(F($"{name}: {form.NameEn} (#{form.Dex}) has no best IVs."));
                }
            }
        }

        return problems.Check("Best IVs", F($"{data.PvpBestIvs.Count:N0} rows"));
    }

    private static ImportCheck KeeperBoxes(ImportedGameData data)
    {
        var problems = new Problems();
        var boxes = data.PvpKeeperBoxes.ToLookup(box => box.League);
        foreach (var (league, _, name) in _leagues.Where(league => league.League != League.Master))
        {
            var dexes = boxes[league].Select(box => box.Dex).ToHashSet();
            var missing = data.Species.Select(species => species.Dex).Where(dex => dex <= KeeperCalculator.MaxDex && !dexes.Contains(dex)).Order().ToList();
            if (missing.Count > 0)
            {
                problems.Add(F($"{name}: {missing.Count} Pokédex number{Plural(missing.Count)} without a keeper box: {Numbers(missing)}."));
            }

            foreach (var box in boxes[league])
            {
                if (!Bars(box.AttackBarMin, box.AttackBarMax) || !Bars(box.DefenseBarMin, box.DefenseBarMax)
                    || !Bars(box.StaminaBarMin, box.StaminaBarMax) || box.JudgedBy.Count == 0)
                {
                    problems.Add(F($"{name}: #{box.Dex}'s box ({box.AttackBarMin}-{box.AttackBarMax} / {box.DefenseBarMin}-{box.DefenseBarMax} / {box.StaminaBarMin}-{box.StaminaBarMax}, judged by {box.JudgedBy.Count} forms) isn't a valid box."));
                }
            }
        }

        return problems.Check("Keeper boxes", F($"{data.PvpKeeperBoxes.Count:N0} boxes"));
    }

    private static ImportCheck RaidRankings(ImportedGameData data, int minimumPerType)
    {
        var problems = new Problems();
        var byType = data.RaidRankings.ToLookup(ranking => ranking.AttackingTypeId, StringComparer.Ordinal);
        foreach (var type in data.Types.OrderBy(type => type.SortOrder))
        {
            var rows = byType[type.Id].ToList();
            if (rows.Count < minimumPerType)
            {
                problems.Add(F($"{type.NameEn} has {rows.Count} entr{(rows.Count == 1 ? "y" : "ies")}, at least {minimumPerType} expected."));
            }

            if (rows.Count > 0)
            {
                Contiguous(problems, type.NameEn, [.. rows.Select(row => row.Rank)]);
            }

            foreach (var row in rows.Where(row => !Positive(row.Dps) || !Positive(row.Tdo) || !Positive(row.Score)))
            {
                problems.Add(F($"{type.NameEn}: {row.FormId}{(row.IsShadow ? " (Shadow)" : "")} has DPS {row.Dps}, TDO {row.Tdo}, score {row.Score} (all above 0 expected)."));
            }
        }

        return problems.Check("Raid rankings", F($"{data.RaidRankings.Count:N0} rows"));
    }

    private static ImportCheck RaidMovesets(ImportedGameData data)
    {
        var problems = new Problems();
        foreach (var group in data.RaidMovesets.GroupBy(moveset => (moveset.FormId, moveset.IsShadow)))
        {
            var name = group.Key.FormId + (group.Key.IsShadow ? " (Shadow)" : "");
            Contiguous(problems, name, [.. group.Select(moveset => moveset.Rank)]);
            foreach (var row in group.Where(row => !Positive(row.Dps) || !Positive(row.Tdo) || !Positive(row.Score)))
            {
                problems.Add(F($"{name}: {row.FastMoveId} + {row.ChargedMoveId} has DPS {row.Dps}, TDO {row.Tdo}, score {row.Score} (all above 0 expected)."));
            }
        }

        return problems.Check("Raid movesets", F($"{data.RaidMovesets.Count:N0} rows"));
    }

    /// <summary>Every Great and Ultra League keeper string run through the game-search simulator against the new boxes.</summary>
    private static ImportCheck KeeperStrings(ImportedGameData data)
    {
        var problems = new Problems();
        var details = new List<string>();
        foreach (var (kind, league, cap, name) in new[]
                 {
                     (KeeperFilterKind.GreatLeague, League.Great, Leagues.Great, "Great League"),
                     (KeeperFilterKind.UltraLeague, League.Ultra, Leagues.Ultra, "Ultra League"),
                 })
        {
            var strings = data.KeeperFilters.Where(filter => filter.Kind == kind && filter.Variant.Length == 0)
                .OrderBy(filter => filter.Part).Select(filter => filter.SearchString).ToList();
            if (strings.Count == 0)
            {
                continue;
            }

            var boxes = data.PvpKeeperBoxes.Where(box => box.League == league).ToDictionary(
                box => box.Dex,
                box => new KeeperBox(new(box.AttackBarMin, box.AttackBarMax), new(box.DefenseBarMin, box.DefenseBarMax), new(box.StaminaBarMin, box.StaminaBarMax)));
            var result = KeeperStringCheck.Check(boxes, strings, cap);
            result.Problems.ToList().ForEach(problem => problems.Add($"{name}: {problem}"));
            details.Add(F($"{name}: {strings.Count} part{Plural(strings.Count)}, {result.Cases:N0} cases"));
        }

        return problems.Check("Keeper strings", details.Count == 0 ? "no strings to check yet (they come with the keeper filters, E04-F01)" : string.Join("; ", details));
    }

    private static void Contiguous(Problems problems, string name, List<int> ranks)
    {
        ranks.Sort();
        for (var i = 0; i < ranks.Count; i++)
        {
            if (ranks[i] != i + 1)
            {
                problems.Add(F($"{name}: the ranks aren't 1 to {ranks.Count} without gaps or repeats (rank {i + 1} is {ranks[i]})."));
                return;
            }
        }
    }

    private static bool Bars(int min, int max) => min >= 0 && max <= Appraisal.Bars - 1 && min <= max;

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;

    private static string Numbers(List<int> dexes) =>
        string.Join(", ", dexes.Take(ProblemsShown).Select(dex => F($"#{dex}"))) + (dexes.Count > ProblemsShown ? F($" and {dexes.Count - ProblemsShown:N0} more") : "");

    private static string Plural(int count) => count == 1 ? "" : "s";

    private static string F(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>A check's problems: the first <see cref="ProblemsShown"/> in full, the rest counted.</summary>
    private sealed class Problems
    {
        private readonly List<string> _shown = [];
        private int _count;

        public void Add(string problem)
        {
            if (++_count <= ProblemsShown)
            {
                _shown.Add(problem);
            }
        }

        public ImportCheck Check(string name, string detail)
        {
            var problems = new List<string>(_shown);
            if (_count > ProblemsShown)
            {
                problems.Add(F($"… and {_count - ProblemsShown:N0} more."));
            }

            return new ImportCheck(name, _count == 0, problems, detail);
        }
    }
}
