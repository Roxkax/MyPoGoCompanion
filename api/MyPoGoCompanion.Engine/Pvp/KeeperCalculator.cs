namespace MyPoGoCompanion.Engine.Pvp;

/// <summary>
/// Which evolutions judge a Pokédex number's box (the Python build's policies; the keeper filters use
/// <see cref="Meta"/>).
/// </summary>
public enum KeeperPolicy
{
    /// <summary>Final evolutions, plus middle stages in the league's top 150.</summary>
    Meta,

    /// <summary>Final evolutions only.</summary>
    Final,

    /// <summary>The form itself and its final evolutions.</summary>
    All,

    /// <summary>The form itself only.</summary>
    Cap,
}

/// <summary>A form's rank-1 spread in a league.</summary>
public sealed record RankOne(string FormId, Ivs Ivs, decimal Level, int Cp, double StatProduct);

/// <summary>A Pokédex number's keeper box in one league.</summary>
/// <param name="JudgedBy">The forms whose 98% keepers chose the box (sorted ids).</param>
/// <param name="Forms">The Pokédex number's own forms (sorted ids).</param>
public sealed record DexKeeperBox(int Dex, BoxFit Fit, IReadOnlyList<string> JudgedBy, IReadOnlyList<RankOne> RankOnes, IReadOnlyList<string> Forms);

/// <summary>
/// The keeper boxes per Pokédex number and league (the Python build's <c>pvp_compute.py</c>): each number is judged
/// by its final evolutions (and, with <see cref="KeeperPolicy.Meta"/>, middle stages in the league's top 150), a
/// spread is a keeper when it reaches 98% of that form's rank-1 stat product, and the box is the best fit for the
/// keepers of all of them together that holds every one's rank-1 spread.
/// </summary>
public sealed class KeeperCalculator
{
    /// <summary>The last Pokédex number covered.</summary>
    public const int MaxDex = 1025;

    /// <summary>How many of PvPoke's league ranking count as "the meta" for middle stages.</summary>
    public const int TopRanked = 150;

    private readonly Dictionary<string, PokemonEntry> _species;
    private readonly CpMultipliers _multipliers;
    private readonly Dictionary<string, HashSet<string>> _finals = new(StringComparer.Ordinal);

    /// <param name="entries">All entries (PvPoke's order); Megas, Shadows, battle-only forms and numbers over <see cref="MaxDex"/> are left out here.</param>
    public KeeperCalculator(IEnumerable<PokemonEntry> entries, CpMultipliers multipliers)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _multipliers = multipliers ?? throw new ArgumentNullException(nameof(multipliers));
        _species = new Dictionary<string, PokemonEntry>(StringComparer.Ordinal);
        Order = [];
        foreach (var entry in entries)
        {
            if (entry.IsMega || entry.IsShadow || PokemonEntry.BattleOnly.Contains(entry.Id) || entry.Dex is < 1 or > MaxDex)
            {
                continue;
            }

            // Like a Python dict: a repeated id keeps its first place and takes the last entry.
            if (!_species.ContainsKey(entry.Id))
            {
                Order.Add(entry.Id);
            }

            _species[entry.Id] = entry;
        }
    }

    /// <summary>The forms that can have a PvP box, in PvPoke's order.</summary>
    public List<string> Order { get; }

    public IReadOnlyDictionary<string, PokemonEntry> Species => _species;

    /// <summary>
    /// The rank-1 spread the screens show for every form in <see cref="Order"/> (<see cref="LeagueStats.Top"/>: a tie
    /// goes to the higher CP); Master League is <see cref="Leagues.Master"/>.
    /// </summary>
    public IEnumerable<RankOne> RankOnes(int cap) =>
        Order.Select(id => RankOneOf(id, LeagueStats.Calculate(_species[id].Stats, cap, _multipliers).Top));

    /// <param name="topRanked">PvPoke's league ranking (species ids in order); only the first 150 count.</param>
    /// <param name="dexes">The Pokédex numbers to box (default: 1–1025).</param>
    /// <exception cref="InvalidOperationException">A Pokédex number has no form to judge it by.</exception>
    public IReadOnlyList<DexKeeperBox> Boxes(int cap, IEnumerable<string> topRanked, KeeperPolicy policy = KeeperPolicy.Meta, IEnumerable<int>? dexes = null)
    {
        ArgumentNullException.ThrowIfNull(topRanked);
        var top = topRanked.Take(TopRanked).ToHashSet(StringComparer.Ordinal);
        // Only what the boxes need is kept per form (rank 1 and the keeper flags), not all 4,096 spreads' numbers.
        var judged = new Dictionary<string, (RankOne RankOne, int Cell, bool[] Keepers)>(StringComparer.Ordinal);
        (RankOne RankOne, int Cell, bool[] Keepers) Info(string id)
        {
            if (!judged.TryGetValue(id, out var info))
            {
                var league = LeagueStats.Calculate(_species[id].Stats, cap, _multipliers);
                info = (RankOneOf(id, league.Best), KeeperBoxes.CellOf(league.BestIndex), league.Keepers(KeeperBoxes.KeepShare));
                judged[id] = info;
            }

            return info;
        }

        var byDex = Order.GroupBy(id => _species[id].Dex).ToDictionary(group => group.Key, group => group.ToList());
        var boxes = new List<DexKeeperBox>(MaxDex);
        foreach (var dex in dexes?.Order().Distinct() ?? Enumerable.Range(1, MaxDex))
        {
            var forms = byDex.GetValueOrDefault(dex, []);
            var sources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in forms)
            {
                var isFinal = Evolutions(id).Count == 0;
                var isMiddle = !isFinal && Parent(id) is not null;
                if (isFinal || policy is KeeperPolicy.All or KeeperPolicy.Cap)
                {
                    sources.Add(id);
                }

                if (!isFinal && policy != KeeperPolicy.Cap)
                {
                    sources.UnionWith(Finals(id, []));
                }

                if (isMiddle && policy == KeeperPolicy.Meta && top.Contains(id))
                {
                    sources.Add(id);
                }
            }

            if (sources.Count == 0)
            {
                throw new InvalidOperationException($"No form found for Pokédex #{dex}.");
            }

            var keepers = new bool[Ivs.Count];
            var required = new HashSet<int>();
            var rankOnes = new List<RankOne>();
            var judgedBy = sources.Order(StringComparer.Ordinal).ToList();
            foreach (var id in judgedBy)
            {
                var (rankOne, cell, keep) = Info(id);
                for (var i = 0; i < keep.Length; i++)
                {
                    keepers[i] |= keep[i];
                }

                required.Add(cell);
                rankOnes.Add(rankOne);
            }

            boxes.Add(new DexKeeperBox(dex, KeeperBoxes.BestBox(keepers, required), judgedBy, rankOnes,
                [.. forms.Order(StringComparer.Ordinal)]));
        }

        return boxes;
    }

    private static RankOne RankOneOf(string id, SpreadInLeague best) => new(id, best.Ivs, best.Level, best.Cp, best.StatProduct);

    private List<string> Evolutions(string id) => [.. _species[id].Evolutions.Where(_species.ContainsKey)];

    private string? Parent(string id) => _species[id].Parent is { } parent && _species.ContainsKey(parent) ? parent : null;

    /// <summary>The final evolutions reachable from a form (itself when it doesn't evolve). Remembered per form, as in Python.</summary>
    private HashSet<string> Finals(string id, IReadOnlyCollection<string> seen)
    {
        if (_finals.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var next = Evolutions(id).Where(e => !seen.Contains(e)).ToList();
        HashSet<string> result;
        if (next.Count == 0)
        {
            result = new HashSet<string>(StringComparer.Ordinal) { id };
        }
        else
        {
            var path = seen.Append(id).ToList();
            result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in next)
            {
                result.UnionWith(Finals(e, path));
            }
        }

        _finals[id] = result;
        return result;
    }
}
