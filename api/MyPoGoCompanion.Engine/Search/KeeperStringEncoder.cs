using System.Globalization;
using MyPoGoCompanion.Engine.Pvp;

namespace MyPoGoCompanion.Engine.Search;

/// <summary>A copyable part of a PvP keeper filter, covering an ordered slice of the Pokédex.</summary>
public sealed record KeeperStringPart(int FromDex, int ToDex, string Text);

/// <summary>Encodes appraisal boxes as Pokémon GO search strings using the legacy keeper filter's split strategy.</summary>
public static class KeeperStringEncoder
{
    public static IReadOnlyList<KeeperStringPart> Encode(IReadOnlyDictionary<int, KeeperBox> boxes, int cap)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cap);
        var entries = boxes.OrderBy(pair => pair.Key).ToArray();
        if (entries.Length == 0)
        {
            return [];
        }

        var maxLength = KeeperStringCheck.MaxLength;
        var best = Split(entries, cap, maxLength);
        var partCount = best.Count;

        // Keep the same number of parts while lowering the longest part's character budget.
        var minimum = best.Max(part => part.Text.Length) / 2;
        while (minimum < maxLength)
        {
            var limit = (minimum + maxLength) / 2;
            var candidate = Split(entries, cap, limit);
            if (candidate.Count <= partCount && candidate.All(part => part.Text.Length <= limit))
            {
                best = candidate;
                maxLength = limit;
            }
            else
            {
                minimum = limit + 1;
            }
        }

        return best;
    }

    private static List<KeeperStringPart> Split(KeyValuePair<int, KeeperBox>[] entries, int cap, int limit)
    {
        var parts = new List<KeeperStringPart>();
        var start = 0;
        while (start < entries.Length)
        {
            var low = start;
            var high = entries.Length - 1;
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (Build(entries.AsSpan(start, middle - start + 1), cap).Length <= limit)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            var text = Build(entries.AsSpan(start, low - start + 1), cap);
            parts.Add(new KeeperStringPart(entries[start].Key, entries[low].Key, text));
            start = low + 1;
        }

        return parts;
    }

    private static string Build(ReadOnlySpan<KeyValuePair<int, KeeperBox>> entries, int cap)
    {
        var numbers = new RangeList();
        foreach (var entry in entries)
        {
            numbers.Add(entry.Key, allowGap: false);
        }

        var clauses = new List<string> { numbers.Finish() };
        clauses.AddRange(Clauses(entries, box => box.Attack, "attack"));
        clauses.AddRange(Clauses(entries, box => box.Defense, "defense"));
        clauses.AddRange(Clauses(entries, box => box.Stamina, "hp"));
        clauses.Add(FormattableString.Invariant($"cp-{cap}"));
        return string.Join('&', clauses);
    }

    /// <summary>Joins distinct Pokédex numbers and consecutive ranges as Pokémon GO search terms.</summary>
    public static string DexRanges(IEnumerable<int> dexes)
    {
        ArgumentNullException.ThrowIfNull(dexes);
        var ranges = new RangeList();
        foreach (var dex in dexes.Distinct().Order())
        {
            ranges.Add(dex, allowGap: false);
        }

        return ranges.Finish();
    }

    private static List<string> Clauses(ReadOnlySpan<KeyValuePair<int, KeeperBox>> entries, Func<KeeperBox, BarRange> select, string stat)
    {
        var bars = new (int Dex, BarRange Range)[entries.Length];
        var present = new HashSet<BarRange>();
        for (var index = 0; index < entries.Length; index++)
        {
            var (dex, box) = entries[index];
            var range = select(box);
            bars[index] = (dex, range);
            present.Add(range);
        }

        var intervals = new List<string>();
        foreach (var range in KeeperBoxes.Ranges.Where(range => range != BarRange.Any && present.Contains(range)))
        {
            intervals.Add(Clause(bars, range, stat, other => other == range));
        }

        var bounds = new List<string>();
        foreach (var lower in present.Select(range => range.Min).Where(min => min > BarRange.Any.Min).Distinct().Order())
        {
            bounds.Add(Clause(bars, new(lower, BarRange.Any.Max), stat, other => other.Min == lower));
        }

        foreach (var upper in present.Select(range => range.Max).Where(max => max < BarRange.Any.Max).Distinct().Order())
        {
            bounds.Add(Clause(bars, new(BarRange.Any.Min, upper), stat, other => other.Max == upper));
        }

        return intervals.Sum(clause => clause.Length + 1) <= bounds.Sum(clause => clause.Length + 1) ? intervals : bounds;
    }

    private static string Clause((int Dex, BarRange Range)[] bars, BarRange rule, string stat, Func<BarRange, bool> mustObey)
    {
        // Commas are OR inside an AND clause. A dex number can bypass a rule its own box doesn't imply.
        // Numbers that already imply the rule can sit inside a bypass range to make it shorter.
        var bypass = new RangeList();
        foreach (var (dex, range) in bars)
        {
            if (mustObey(range))
            {
                bypass.Close();
            }
            else if (range.Min < rule.Min || range.Max > rule.Max)
            {
                bypass.Add(dex, allowGap: true);
            }
        }

        var numbers = bypass.Finish();
        var term = rule.Min == rule.Max
            ? FormattableString.Invariant($"{rule.Min}{stat}")
            : FormattableString.Invariant($"{rule.Min}-{rule.Max}{stat}");
        return numbers.Length == 0 ? term : $"{numbers},{term}";
    }

    private sealed class RangeList
    {
        private readonly List<string> _ranges = [];
        private int? _first;
        private int _last;

        public void Add(int dex, bool allowGap)
        {
            if (!allowGap && dex != _last + 1)
            {
                Close();
            }

            _first ??= dex;
            _last = dex;
        }

        public void Close()
        {
            if (_first is { } first)
            {
                _ranges.Add(first == _last
                    ? first.ToString(CultureInfo.InvariantCulture)
                    : FormattableString.Invariant($"{first}-{_last}"));
                _first = null;
            }
        }

        public string Finish()
        {
            Close();
            return string.Join(',', _ranges);
        }
    }
}
