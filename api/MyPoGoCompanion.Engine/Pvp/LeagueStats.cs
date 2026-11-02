namespace MyPoGoCompanion.Engine.Pvp;

/// <summary>GO Battle League CP caps.</summary>
public static class Leagues
{
    public const int Great = 1500;
    public const int Ultra = 2500;

    /// <summary>Master League has no cap.</summary>
    public const int Master = int.MaxValue;

    /// <summary>The highest level the PvP numbers use (the Python build's; Best Buddy's 51 is left out).</summary>
    public const decimal MaxLevel = 50m;
}

/// <summary>One IV spread at its best level under a league's cap.</summary>
public readonly record struct SpreadInLeague(Ivs Ivs, decimal Level, int Cp, double StatProduct);

/// <summary>
/// Every one of the 4,096 IV spreads of a form, each at the highest level ≤ 50 that stays under the CP cap, with its
/// stat product <c>Atk × Def × floor(HP)</c> (the Python build's <c>league_stats</c>). A spread that is over the cap
/// even at level 1 stays at level 1.
/// </summary>
public sealed class LeagueStats
{
    private readonly double[] _statProduct = new double[Ivs.Count];
    private readonly int[] _levelIndex = new int[Ivs.Count];
    private readonly int[] _cp = new int[Ivs.Count];
    private int[]? _order;

    private LeagueStats(BaseStats stats, int cap, int best)
    {
        Stats = stats;
        Cap = cap;
        BestIndex = best;
    }

    public BaseStats Stats { get; }

    public int Cap { get; }

    /// <summary>
    /// Index (<see cref="Ivs.Index"/>) of the keeper filters' rank-1 spread: the highest stat product, the first in
    /// <see cref="Ivs.All"/> order on a tie (the Python build's rule, which the keeper boxes keep).
    /// </summary>
    public int BestIndex { get; private set; }

    public SpreadInLeague Best => this[BestIndex];

    /// <summary>
    /// Index of the rank-1 spread the screens show: the highest stat product; on a tie (HP rounds down, so 15/15/14
    /// and 15/15/15 often tie) the higher CP, then the first in order.
    /// </summary>
    public int TopIndex
    {
        get
        {
            var top = BestIndex;
            var product = _statProduct[top];
            for (var index = top + 1; index < Ivs.Count; index++)
            {
                if (_statProduct[index] == product && _cp[index] > _cp[top])
                {
                    top = index;
                }
            }

            return top;
        }
    }

    public SpreadInLeague Top => this[TopIndex];

    public SpreadInLeague this[int index] =>
        new(Ivs.FromIndex(index), CpMultipliers.LevelAt(_levelIndex[index]), _cp[index], _statProduct[index]);

    public SpreadInLeague this[Ivs ivs] => this[ivs.Index];

    public double StatProduct(int index) => _statProduct[index];

    public static LeagueStats Calculate(BaseStats stats, int cap, CpMultipliers multipliers, decimal maxLevel = Leagues.MaxLevel)
    {
        ArgumentNullException.ThrowIfNull(multipliers);
        var levels = multipliers.IndexOf(maxLevel) + 1;
        var result = new LeagueStats(stats, cap, 0);
        var squares = new double[levels];
        for (var i = 0; i < levels; i++)
        {
            squares[i] = multipliers[i] * multipliers[i];
        }

        var best = 0;
        for (var index = 0; index < Ivs.Count; index++)
        {
            var ivs = Ivs.FromIndex(index);
            var a = stats.Attack + ivs.Attack;
            var d = stats.Defense + ivs.Defense;
            var h = stats.Stamina + ivs.Stamina;
            var root = Math.Sqrt((double)d * h);
            int CpAt(int level) => Math.Max(Cp.Minimum, (int)Math.Floor(a * root * squares[level] / 10));

            // CP never drops as the level rises, so the highest level under the cap is found by halving.
            var levelIndex = 0;
            if (CpAt(0) <= cap)
            {
                int low = 0, high = levels - 1;
                while (low < high)
                {
                    var mid = (low + high + 1) / 2;
                    if (CpAt(mid) <= cap)
                    {
                        low = mid;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }

                levelIndex = low;
            }

            var m = multipliers[levelIndex];
            result._levelIndex[index] = levelIndex;
            result._cp[index] = CpAt(levelIndex);
            result._statProduct[index] = a * m * (d * m) * Math.Max(Math.Floor(h * m), Cp.Minimum);
            if (result._statProduct[index] > result._statProduct[best])
            {
                best = index;
            }
        }

        result.BestIndex = best;
        return result;
    }

    /// <summary>
    /// The spread's rank: 1 + the number of spreads with a higher stat product (equal stat products share a rank).
    /// </summary>
    public int RankOf(Ivs ivs)
    {
        var product = _statProduct[ivs.Index];
        return 1 + _statProduct.Count(other => other > product);
    }

    /// <summary>The spread's stat product as a share of rank 1's (1 = rank 1).</summary>
    public double ShareOfBest(Ivs ivs) => _statProduct[ivs.Index] / _statProduct[BestIndex];

    /// <summary>
    /// The spreads from rank 1 down: highest stat product first; equal ones by higher CP, then in
    /// <see cref="Ivs.All"/> order (so the first is <see cref="Top"/>).
    /// </summary>
    public IEnumerable<SpreadInLeague> Ranking()
    {
        _order ??= [.. Enumerable.Range(0, Ivs.Count).OrderByDescending(index => _statProduct[index]).ThenByDescending(index => _cp[index])];
        return _order.Select(index => this[index]);
    }

    /// <summary>The spreads whose stat product is at least <paramref name="share"/> of rank 1's (the keeper filters' 98%).</summary>
    public bool[] Keepers(double share)
    {
        var limit = share * _statProduct[BestIndex];
        return [.. _statProduct.Select(product => product >= limit)];
    }
}
