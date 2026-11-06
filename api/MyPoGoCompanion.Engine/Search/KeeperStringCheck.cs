using System.Globalization;
using MyPoGoCompanion.Engine.Pvp;

namespace MyPoGoCompanion.Engine.Search;

/// <summary>What checking a league's keeper strings found.</summary>
/// <param name="Cases">Pokémon the simulator tried (Pokédex number × appraisal cell × CP).</param>
/// <param name="Problems">What is wrong, in plain words; empty when the strings are right.</param>
public sealed record KeeperStringResult(int Cases, IReadOnlyList<string> Problems)
{
    public bool Passed => Problems.Count == 0;
}

/// <summary>
/// Checks a league's keeper strings the way the Python build's <c>verify</c> did, with <see cref="GameSearch"/>: for
/// every Pokédex number 1–1,100 (beyond the last one too, which must never match), every appraisal cell (125) and a
/// CP at and just over the league's cap, exactly one of the strings must match a Pokémon inside its keeper box under
/// the cap, and none may match anything else. A string may be at most 1,950 characters (iOS text replacement keeps
/// 2,000).
/// </summary>
public static class KeeperStringCheck
{
    /// <summary>The longest string the app publishes.</summary>
    public const int MaxLength = 1950;

    /// <summary>The last Pokédex number checked (numbers above the last real one must match nothing).</summary>
    public const int LastDexChecked = 1100;

    /// <summary>How many wrong Pokémon are described before the rest are only counted.</summary>
    public const int ProblemsShown = 5;

    /// <param name="boxes">The keeper box per Pokédex number (numbers without one keep nothing).</param>
    /// <param name="strings">The league's strings, in order (part 1, 2…).</param>
    /// <param name="cap">The league's CP cap (1,500 or 2,500).</param>
    public static KeeperStringResult Check(IReadOnlyDictionary<int, KeeperBox> boxes, IReadOnlyList<string> strings, int cap)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(strings);
        var problems = new List<string>();
        var searches = new List<GameSearch>();
        for (var i = 0; i < strings.Count; i++)
        {
            if (strings[i].Length > MaxLength)
            {
                problems.Add(Format($"Part {i + 1} is {strings[i].Length:N0} characters long (at most {MaxLength:N0})."));
            }

            try
            {
                searches.Add(GameSearch.Parse(strings[i]));
            }
            catch (UnsupportedSearchException exception)
            {
                problems.Add($"Part {i + 1}: {exception.Message}");
            }
        }

        if (problems.Count > 0)
        {
            return new KeeperStringResult(0, problems);
        }

        int cases = 0, wrong = 0;
        int[] cps = [cap, cap + 1];
        for (var dex = 1; dex <= LastDexChecked; dex++)
        {
            var box = boxes.TryGetValue(dex, out var found) ? found : (KeeperBox?)null;
            for (var cell = 0; cell < 125; cell++)
            {
                foreach (var cp in cps)
                {
                    cases++;
                    var subject = new SearchSubject(dex, cell / 25, cell / 5 % 5, cell % 5, cp);
                    var hits = 0;
                    foreach (var search in searches)
                    {
                        if (search.Matches(subject))
                        {
                            hits++;
                        }
                    }

                    var expected = cp <= cap && box?.Contains(cell) == true ? 1 : 0;
                    if (hits == expected)
                    {
                        continue;
                    }

                    if (++wrong <= ProblemsShown)
                    {
                        var side = cp <= cap ? "under" : "over";
                        problems.Add(Format(
                            $"Pokédex #{dex} with appraisal bars {subject.AttackBar}/{subject.DefenseBar}/{subject.HpBar} at CP {cp:N0} ({side} the cap) matched {Times(hits)}, expected {Times(expected)}."));
                    }
                }
            }
        }

        if (wrong > ProblemsShown)
        {
            problems.Add(Format($"… and {wrong - ProblemsShown:N0} more."));
        }

        return new KeeperStringResult(cases, problems);
    }

    private static string Times(int count) => count switch
    {
        0 => "no string",
        1 => "1 string",
        _ => Format($"{count} strings"),
    };

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
