using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Engine.Pvp;
using MyPoGoCompanion.Engine.Raids;

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
        foreach (var (league, cap) in _pvpLeagues)
        {
            foreach (var one in keepers.RankOnes(cap).Where(one => formIds.Contains(one.FormId)))
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

            foreach (var box in boxes)
            {
                data.PvpKeeperBoxes.Add(new PvpKeeperBox
                {
                    League = league,
                    Dex = box.Dex,
                    AttackBarMin = box.Fit.Box.Attack.Min,
                    AttackBarMax = box.Fit.Box.Attack.Max,
                    DefenseBarMin = box.Fit.Box.Defense.Min,
                    DefenseBarMax = box.Fit.Box.Defense.Max,
                    StaminaBarMin = box.Fit.Box.Stamina.Min,
                    StaminaBarMax = box.Fit.Box.Stamina.Max,
                    JudgedBy = [.. box.JudgedBy],
                });
            }
        }

        var raids = new RaidRanker(inputs.Entries, inputs.Moves, inputs.Multipliers);
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
}
