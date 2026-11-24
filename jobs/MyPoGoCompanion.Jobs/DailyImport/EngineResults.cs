using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Engine.Pvp;
using MyPoGoCompanion.Engine.Raids;
using MyPoGoCompanion.Engine.Search;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>
/// The calculation engine's results the daily import stores (decision 2026-11-02): the rank-1 PvP IVs per form
/// (Great, Ultra, Master), the keeper box per Pokédex number (Great, Ultra), the raid ranking per attacking type
/// and every form's raid movesets. Rows for forms or moves the import doesn't keep are left out.
/// </summary>
public static class EngineResults
{
    private static readonly (League League, int Cap)[] _pvpLeagues =
        [(League.Great, Leagues.Great), (League.Ultra, Leagues.Ultra), (League.Master, Leagues.Master)];

    public static void Add(
        ImportedGameData data,
        EngineInputs inputs,
        IReadOnlyList<(League League, List<PvpokeRanking> Rows)> rankings)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(rankings);
        var formIds = data.Forms.Select(form => form.Id).ToHashSet(StringComparer.Ordinal);
        var moveIds = data.Moves.Select(move => move.Id).ToHashSet(StringComparer.Ordinal);
        // Every Pokédex number up to 1,025 the import has (all of them with the real files).
        var dexes = data.Species.Select(species => species.Dex).Where(dex => dex <= KeeperCalculator.MaxDex).ToHashSet();

        var keepers = new KeeperCalculator(inputs.Entries, inputs.Multipliers);
        var raids = new RaidRanker(inputs.Entries, inputs.Moves, inputs.Multipliers);
        var megas = inputs.Entries
            .Where(entry => entry.IsMega && !entry.IsShadow && formIds.Contains(entry.FormId))
            .DistinctBy(entry => entry.FormId, StringComparer.Ordinal)
            .ToList();
        var releasedMegaFamilies = megas.Where(mega => mega.Released)
            .SelectMany(mega => raids.PreEvolutions(mega).Prepend(mega.Dex).Distinct().Select(dex => (Dex: dex, Mega: mega)))
            .ToLookup(pair => pair.Dex, pair => pair.Mega);
        foreach (var (league, cap) in _pvpLeagues)
        {
            var megaLeagues = megas.ToDictionary(mega => mega.FormId,
                mega => LeagueStats.Calculate(mega.Stats, cap, inputs.Multipliers), StringComparer.Ordinal);
            var megaRankOnes = megaLeagues.Select(pair =>
            {
                var top = pair.Value.Top;
                return new RankOne(pair.Key, top.Ivs, top.Level, top.Cp, top.StatProduct);
            });
            foreach (var one in keepers.RankOnes(cap).Where(one => formIds.Contains(one.FormId)).Concat(megaRankOnes))
            {
                data.PvpBestIvs.Add(new PvpBestIvs
                {
                    League = league,
                    FormId = one.FormId,
                    AttackIv = one.Ivs.Attack,
                    DefenseIv = one.Ivs.Defense,
                    StaminaIv = one.Ivs.Stamina,
                    Level = one.Level,
                    Cp = one.Cp,
                    StatProduct = one.StatProduct,
                });
            }

            if (league == League.Master)
            {
                continue;
            }

            var top = rankings.FirstOrDefault(r => r.League == league).Rows?.Select(row => row.SpeciesId) ?? [];
            IReadOnlyList<DexKeeperBox> boxes;
            try
            {
                boxes = keepers.Boxes(cap, top, dexes: dexes);
            }
            catch (InvalidOperationException exception)
            {
                throw new ImportException($"The {league} League keeper boxes can't be calculated: {exception.Message}", exception);
            }

            var finalBoxes = new Dictionary<int, KeeperBox>(boxes.Count);
            var megaBoxes = new Dictionary<string, KeeperBox>(StringComparer.Ordinal);
            foreach (var entry in boxes)
            {
                var box = entry.Fit.Box;
                var judges = new List<string>(entry.JudgedBy);
                foreach (var mega in releasedMegaFamilies[entry.Dex])
                {
                    if (!megaBoxes.TryGetValue(mega.FormId, out var megaBox))
                    {
                        var stats = megaLeagues[mega.FormId];
                        // Include the rank-1 tie-break the Pokémon page displays, too.
                        megaBox = KeeperBoxes.BestBox(stats.Keepers(KeeperBoxes.KeepShare),
                            [KeeperBoxes.CellOf(stats.BestIndex), Appraisal.Cell(stats.Top.Ivs)]).Box;
                        megaBoxes.Add(mega.FormId, megaBox);
                    }

                    box = new KeeperBox(Cover(box.Attack, megaBox.Attack), Cover(box.Defense, megaBox.Defense), Cover(box.Stamina, megaBox.Stamina));
                    judges.Add(mega.FormId);
                }

                finalBoxes.Add(entry.Dex, box);
                data.PvpKeeperBoxes.Add(new PvpKeeperBox
                {
                    League = league,
                    Dex = entry.Dex,
                    AttackBarMin = box.Attack.Min,
                    AttackBarMax = box.Attack.Max,
                    DefenseBarMin = box.Defense.Min,
                    DefenseBarMax = box.Defense.Max,
                    StaminaBarMin = box.Stamina.Min,
                    StaminaBarMax = box.Stamina.Max,
                    JudgedBy = [.. judges.Order(StringComparer.Ordinal)],
                });
            }

            var partNumber = 0;
            foreach (var part in KeeperStringEncoder.Encode(finalBoxes, cap))
            {
                data.KeeperFilters.Add(new KeeperFilter
                {
                    Kind = league == League.Great ? KeeperFilterKind.GreatLeague : KeeperFilterKind.UltraLeague,
                    Part = ++partNumber,
                    FromDex = part.FromDex,
                    ToDex = part.ToDex,
                    SearchString = part.Text,
                });
            }
        }

        bool Keeps(PokemonEntry pokemon, RaidScore score) =>
            formIds.Contains(pokemon.FormId) && moveIds.Contains(score.FastMoveId) && moveIds.Contains(score.ChargedMoveId);

        foreach (var type in raids.RankAll())
        {
            var seen = new HashSet<(string, bool)>();
            var rank = 0;
            foreach (var entry in type.Ranked.Where(entry => Keeps(entry.Pokemon, entry.Best)))
            {
                if (!seen.Add((entry.Pokemon.FormId, entry.Pokemon.IsShadow)))
                {
                    continue;
                }

                data.RaidRankings.Add(new RaidRanking
                {
                    AttackingTypeId = type.Type,
                    FormId = entry.Pokemon.FormId,
                    IsShadow = entry.Pokemon.IsShadow,
                    Rank = ++rank,
                    FastMoveId = entry.Best.FastMoveId,
                    ChargedMoveId = entry.Best.ChargedMoveId,
                    Dps = entry.Best.Dps,
                    Tdo = entry.Best.Tdo,
                    Score = entry.Best.Score,
                });
            }
        }

        var done = new HashSet<(string, bool)>();
        foreach (var pokemon in inputs.Entries.Where(p => formIds.Contains(p.FormId) && !PokemonEntry.BattleOnly.Contains(p.Id)))
        {
            if (!done.Add((pokemon.FormId, pokemon.IsShadow)))
            {
                continue;
            }

            var rank = 0;
            var combinations = new HashSet<(string, string)>();
            foreach (var score in raids.Movesets(pokemon).Where(score => Keeps(pokemon, score)))
            {
                if (!combinations.Add((score.FastMoveId, score.ChargedMoveId)))
                {
                    continue;
                }

                data.RaidMovesets.Add(new RaidMoveset
                {
                    FormId = pokemon.FormId,
                    IsShadow = pokemon.IsShadow,
                    FastMoveId = score.FastMoveId,
                    ChargedMoveId = score.ChargedMoveId,
                    Rank = ++rank,
                    Dps = score.Dps,
                    Tdo = score.Tdo,
                    Score = score.Score,
                });
            }
        }
    }

    private static BarRange Cover(BarRange first, BarRange second) =>
        new(Math.Min(first.Min, second.Min), Math.Max(first.Max, second.Max));
}
