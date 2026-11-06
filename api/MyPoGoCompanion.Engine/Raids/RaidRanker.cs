using System.Text.RegularExpressions;
using MyPoGoCompanion.Engine.Types;

namespace MyPoGoCompanion.Engine.Raids;

/// <summary>An entry's place in one attacking type's raid list.</summary>
/// <param name="Top">Among the first 10 Pokédex numbers (Megas aside): the type's picks.</param>
/// <param name="MegaPick">A Mega or Primal scoring above the 10th pick.</param>
/// <param name="PreEvolutions">Pokédex numbers it evolves from (for a Mega: its base form's).</param>
public sealed record RaidEntryScore(
    PokemonEntry Pokemon, RaidScore Best, bool EliteFast, bool EliteCharged, IReadOnlyList<int> PreEvolutions, bool Top, bool MegaPick);

/// <summary>One attacking type's raid list.</summary>
/// <param name="Tenth">The 10th pick's score (0 when fewer than 10 Pokédex numbers qualify).</param>
public sealed record TypeRanking(string Type, double Tenth, IReadOnlyList<RaidEntryScore> Ranked);

/// <summary>
/// Raid rankings (the Python build's <c>raid_rank.py</c>): every released entry, level 40, 15/15/15, against the
/// neutral target, scored with its best moveset whose fast and charged moves are both of the type. Per type, the
/// picks are the first 10 Pokédex numbers among regular and Shadow forms and every Mega above the 10th.
/// </summary>
public sealed partial class RaidRanker
{
    /// <summary>How many Pokédex numbers a type's list picks.</summary>
    public const int TopDex = 10;

    /// <summary>Moves left out of the rankings (Return only after purifying, Frustration only on Shadows).</summary>
    public static IReadOnlySet<string> ExcludedMoves { get; } = new HashSet<string>(StringComparer.Ordinal) { "RETURN", "FRUSTRATION" };

    private readonly IReadOnlyList<PokemonEntry> _all;
    private readonly Dictionary<string, PokemonEntry> _byId;
    private readonly RaidMoveBook _moves;
    private readonly double _cpm;

    /// <param name="entries">All entries in PvPoke's order.</param>
    public RaidRanker(IReadOnlyList<PokemonEntry> entries, RaidMoveBook moves, CpMultipliers multipliers)
    {
        _all = entries ?? throw new ArgumentNullException(nameof(entries));
        _moves = moves ?? throw new ArgumentNullException(nameof(moves));
        ArgumentNullException.ThrowIfNull(multipliers);
        _cpm = multipliers.At(RaidFormula.Level);
        _byId = new Dictionary<string, PokemonEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            _byId[entry.Id] = entry;
        }
    }

    /// <summary>Released entries that can be in a raid team (battle-only forms left out).</summary>
    public IEnumerable<PokemonEntry> Attackers => _all.Where(p => p.Released && !PokemonEntry.BattleOnly.Contains(p.Id));

    /// <summary>The lists for all 18 types, in the game's type order.</summary>
    public IReadOnlyList<TypeRanking> RankAll() => [.. TypeChart.Types.Select(Rank)];

    public TypeRanking Rank(string type)
    {
        var scored = new List<(PokemonEntry Pokemon, RaidScore Best)>();
        foreach (var p in Attackers)
        {
            var fast = Moves(p.FastMoves, true).Where(m => m.Type == type).ToList();
            var charged = Moves(p.ChargedMoves, false).Where(m => m.Type == type).ToList();
            if (fast.Count == 0 || charged.Count == 0)
            {
                continue;
            }

            var stats = RaidFormula.Stats(p, _cpm);
            RaidScore? best = null;
            foreach (var f in fast)
            {
                foreach (var c in charged)
                {
                    var score = RaidFormula.Neutral(p, stats, f, c);
                    if (best is null || score.Score > best.Score)
                    {
                        best = score;
                    }
                }
            }

            scored.Add((p, best!));
        }

        // Highest score first; equal scores keep PvPoke's order (Python's sort is stable, so is OrderBy).
        scored = [.. scored.OrderByDescending(s => s.Best.Score)];
        var dexes = new List<int>();
        double? tenth = null;
        foreach (var (p, best) in scored)
        {
            if (p.IsMega || dexes.Contains(p.Dex))
            {
                continue;
            }

            dexes.Add(p.Dex);
            if (dexes.Count == TopDex)
            {
                tenth = best.Score;
                break;
            }
        }

        var cut = tenth ?? 0;
        var ranked = scored.Select(s =>
        {
            var elite = s.Pokemon.EliteMoves.ToHashSet(StringComparer.Ordinal);
            return new RaidEntryScore(s.Pokemon, s.Best, elite.Contains(s.Best.FastMoveId), elite.Contains(s.Best.ChargedMoveId),
                PreEvolutions(s.Pokemon), !s.Pokemon.IsMega && dexes.Contains(s.Pokemon.Dex), s.Pokemon.IsMega && s.Best.Score > cut);
        }).ToList();
        return new TypeRanking(type, cut, ranked);
    }

    /// <summary>
    /// Every fast + charged combination of an entry against the neutral target, best first (the Pokémon page's moveset
    /// ranking). Moves without raid numbers are left out; Return and Frustration count here.
    /// </summary>
    public IReadOnlyList<RaidScore> Movesets(PokemonEntry pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var stats = RaidFormula.Stats(pokemon, _cpm);
        var fast = Moves(pokemon.FastMoves, true, excluded: false).ToList();
        var charged = Moves(pokemon.ChargedMoves, false, excluded: false).ToList();
        // A moveset that deals no damage and gains no energy (Ditto's Transform + Struggle) has no score: left out.
        return [.. (from f in fast from c in charged select RaidFormula.Neutral(pokemon, stats, f, c))
            .Where(s => double.IsFinite(s.Score)).OrderByDescending(s => s.Score)];
    }

    /// <summary>The Pokédex numbers picked by any type, and their pre-evolutions not picked themselves (both sorted).</summary>
    public static (IReadOnlyList<int> Attackers, IReadOnlyList<int> PreEvolutions) Selection(IEnumerable<TypeRanking> rankings)
    {
        ArgumentNullException.ThrowIfNull(rankings);
        var attackers = new SortedSet<int>();
        var pre = new SortedSet<int>();
        foreach (var r in rankings.SelectMany(t => t.Ranked).Where(r => r.Top || r.MegaPick))
        {
            attackers.Add(r.Pokemon.Dex);
            pre.UnionWith(r.PreEvolutions);
        }

        pre.ExceptWith(attackers);
        return ([.. attackers], [.. pre]);
    }

    /// <summary>The Pokédex numbers of the forms an entry evolves from, nearest first.</summary>
    public IReadOnlyList<int> PreEvolutions(PokemonEntry pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        var p = pokemon;
        if (p.IsMega)
        {
            // PvPoke's Mega entries often lack family data: use the base form.
            var baseId = MegaSuffix().Replace(p.Id, "");
            p = _byId.GetValueOrDefault(baseId)
                ?? _all.FirstOrDefault(q => q.Dex == pokemon.Dex && !q.IsMega && !q.IsShadow)
                ?? p;
        }

        var result = new List<int>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var parent = p.Parent;
        while (parent is not null && _byId.TryGetValue(parent, out var q) && seen.Add(parent))
        {
            result.Add(q.Dex);
            parent = q.Parent;
        }

        return result;
    }

    private IEnumerable<RaidMove> Moves(IEnumerable<string> ids, bool fast, bool excluded = true)
    {
        foreach (var id in ids)
        {
            if (excluded && ExcludedMoves.Contains(id))
            {
                continue;
            }

            if ((fast ? _moves.Fast(id) : _moves.Charged(id)) is { } move)
            {
                yield return move;
            }
        }
    }

    [GeneratedRegex("_(mega|primal)(_[xyz])?$")]
    private static partial Regex MegaSuffix();
}
