namespace MyPoGoCompanion.Engine.Pvp;

/// <summary>A range of appraisal bars (0–4) for one stat.</summary>
public readonly record struct BarRange(int Min, int Max)
{
    public static BarRange Any { get; } = new(0, Appraisal.Bars - 1);

    public bool Contains(int bar) => bar >= Min && bar <= Max;
}

/// <summary>Appraisal bars to keep: one range per stat. The game's search can ask for exactly this.</summary>
public readonly record struct KeeperBox(BarRange Attack, BarRange Defense, BarRange Stamina)
{
    public bool Contains(int cell) =>
        Attack.Contains(cell / 25) && Defense.Contains(cell / 5 % 5) && Stamina.Contains(cell % 5);

    public bool Contains(Ivs ivs) => Contains(Appraisal.Cell(ivs));
}

/// <summary>The best box for a set of keepers, with how well it fits them.</summary>
/// <param name="F1">2·TP / (2·TP + FP + FN): 1 when the box holds exactly the keepers.</param>
/// <param name="TruePositives">Keepers inside the box.</param>
/// <param name="InBox">Spreads inside the box.</param>
/// <param name="Keepers">Keepers in all.</param>
public sealed record BoxFit(KeeperBox Box, double F1, int TruePositives, int InBox, int Keepers);

/// <summary>
/// Chooses appraisal boxes (the Python build's <c>best_box</c>): of the 3,375 boxes (15 bar ranges per stat), the one
/// with the best F1 against the keepers that contains every required cell (each judging form's rank-1 spread); on
/// a tie the box holding the fewest spreads, then the first in order.
/// </summary>
public static class KeeperBoxes
{
    /// <summary>The share of rank 1's stat product a keeper needs (98%).</summary>
    public const double KeepShare = 0.98;

    private const int Cells = 125;

    // Static fields are set in the order written: each line below only uses the ones above it.
    private static readonly int[] _cellOfSpread = [.. Ivs.All.Select(Appraisal.Cell)];

    /// <summary>The 15 bar ranges per stat in the Python order: (0,0), (0,1) … (0,4), (1,1) … (4,4).</summary>
    public static IReadOnlyList<BarRange> Ranges { get; } =
        [.. from min in Enumerable.Range(0, Appraisal.Bars) from max in Enumerable.Range(min, Appraisal.Bars - min) select new BarRange(min, max)];

    /// <summary>All 3,375 boxes, Attack range outermost.</summary>
    public static IReadOnlyList<KeeperBox> All { get; } =
        [.. from a in Ranges from d in Ranges from h in Ranges select new KeeperBox(a, d, h)];

    /// <summary>Spreads per appraisal cell.</summary>
    internal static int[] CellSizes { get; } = Count(Enumerable.Repeat(true, Ivs.Count).ToArray());

    private static readonly int[,,] _sizePrefix = Prefix(CellSizes);
    private static readonly int[] _boxSizes = [.. All.Select(box => Sum(_sizePrefix, box))];

    public static int CellOf(int spreadIndex) => _cellOfSpread[spreadIndex];

    /// <param name="keepers">One flag per spread (<see cref="Ivs.Index"/> order).</param>
    /// <param name="requiredCells">Cells the box must contain.</param>
    public static BoxFit BestBox(IReadOnlyList<bool> keepers, IReadOnlyCollection<int> requiredCells)
    {
        ArgumentNullException.ThrowIfNull(keepers);
        ArgumentNullException.ThrowIfNull(requiredCells);
        if (keepers.Count != Ivs.Count)
        {
            throw new ArgumentException($"One flag per IV spread ({Ivs.Count}) is needed.", nameof(keepers));
        }

        var perCell = Count(keepers);
        var prefix = Prefix(perCell);
        var total = perCell.Sum();
        var best = -1;
        double bestF1 = 0;
        int bestTp = 0;
        for (var i = 0; i < All.Count; i++)
        {
            var box = All[i];
            var tp = Sum(prefix, box);
            var fp = _boxSizes[i] - tp;
            var fn = total - tp;
            var f1 = requiredCells.All(box.Contains) ? 2.0 * tp / Math.Max((2 * tp) + fp + fn, 1) : -1.0;

            // Best F1, then the tightest box, then the first (numpy's lexsort is stable).
            if (best < 0 || f1 > bestF1 || (f1 == bestF1 && _boxSizes[i] < _boxSizes[best]))
            {
                best = i;
                bestF1 = f1;
                bestTp = tp;
            }
        }

        return new BoxFit(All[best], bestF1, bestTp, _boxSizes[best], total);
    }

    private static int[] Count(IReadOnlyList<bool> keepers)
    {
        var perCell = new int[Cells];
        for (var i = 0; i < keepers.Count; i++)
        {
            if (keepers[i])
            {
                perCell[_cellOfSpread[i]]++;
            }
        }

        return perCell;
    }

    /// <summary>3-D running totals over the 5 × 5 × 5 cells, so a box's count is a few look-ups.</summary>
    private static int[,,] Prefix(int[] perCell)
    {
        var prefix = new int[Appraisal.Bars + 1, Appraisal.Bars + 1, Appraisal.Bars + 1];
        for (var a = 1; a <= Appraisal.Bars; a++)
        {
            for (var d = 1; d <= Appraisal.Bars; d++)
            {
                for (var h = 1; h <= Appraisal.Bars; h++)
                {
                    prefix[a, d, h] = perCell[((a - 1) * 25) + ((d - 1) * 5) + (h - 1)]
                        + prefix[a - 1, d, h] + prefix[a, d - 1, h] + prefix[a, d, h - 1]
                        - prefix[a - 1, d - 1, h] - prefix[a - 1, d, h - 1] - prefix[a, d - 1, h - 1]
                        + prefix[a - 1, d - 1, h - 1];
                }
            }
        }

        return prefix;
    }

    private static int Sum(int[,,] p, KeeperBox box)
    {
        int a0 = box.Attack.Min, a1 = box.Attack.Max + 1;
        int d0 = box.Defense.Min, d1 = box.Defense.Max + 1;
        int h0 = box.Stamina.Min, h1 = box.Stamina.Max + 1;
        return p[a1, d1, h1] - p[a0, d1, h1] - p[a1, d0, h1] - p[a1, d1, h0]
            + p[a0, d0, h1] + p[a0, d1, h0] + p[a1, d0, h0] - p[a0, d0, h0];
    }
}
