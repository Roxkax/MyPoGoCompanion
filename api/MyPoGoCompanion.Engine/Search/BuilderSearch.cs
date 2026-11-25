using System.Globalization;
using System.Text.RegularExpressions;
using MyPoGoCompanion.Engine.Types;

namespace MyPoGoCompanion.Engine.Search;

/// <summary>Checks storage searches without requiring the player's inventory and prepares pasteable parts.</summary>
public static partial class BuilderSearch
{
    public const int CharacterLimit = 1950;
    public const int InputLimit = 19500;
    private const int MaxParts = 128;
    private static readonly char[] _and = ['&', '|'];
    private static readonly char[] _or = [',', ';', ':'];
    private static readonly HashSet<string> _keywords = new(StringComparer.Ordinal)
    {
        "shiny", "lucky", "shadow", "purified", "costume", "background", "locationbackground",
        "legendary", "mythical", "ultrabeast", "ultra beasts", "traded", "hatched", "eggsonly",
        "@special", "@weather", "evolve", "evolvenew", "evolvequest", "tradeevolve", "megaevolve", "item",
        "dynamax", "gigantamax", "fusion", "favorite", "defender", "hypertraining", "xl", "xxl", "xs", "xxs",
        "kanto", "johto", "hoenn", "sinnoh", "unova", "kalos", "alola", "galar", "hisui", "paldea",
    };

    public enum SearchCheckStatus
    {
        Valid,
        Unverified,
        Invalid,
        CannotSplit,
        TooLong,
    }

    public sealed record SearchCheckResult(SearchCheckStatus Status, IReadOnlyList<string> Parts, string? Term = null);

    public static SearchCheckResult Check(string? expression, Func<string, bool>? knownName = null)
    {
        if (expression is null || string.IsNullOrWhiteSpace(expression))
        {
            return new(SearchCheckStatus.Invalid, []);
        }

        if (expression.Length > InputLimit)
        {
            return new(SearchCheckStatus.TooLong, []);
        }

        var clauses = expression.Split(_and).Select(clause => clause.Split(_or, StringSplitOptions.TrimEntries)).ToArray();
        var flags = new HashSet<string>(StringComparer.Ordinal);
        string? unverified = null;
        foreach (var term in clauses.SelectMany(clause => clause))
        {
            var body = (term.StartsWith('!') ? term[1..] : term).ToLowerInvariant();
            if (body.Length == 0 || char.IsWhiteSpace(body[0]) || body.IndexOfAny(['!', '(', ')', '\r', '\n', '\t']) >= 0)
            {
                return new(SearchCheckStatus.Invalid, [], term);
            }

            var numeric = NumberTerm().Match(body.StartsWith("distance ", StringComparison.Ordinal)
                ? "distance" + body[8..].TrimStart()
                : body);
            if (numeric.Success)
            {
                if (!ValidRange(numeric))
                {
                    return new(SearchCheckStatus.Invalid, [], term);
                }

                var suffix = numeric.Groups["suffix"].Value;
                var prefix = numeric.Groups["prefix"].Value;
                if (suffix != "*" && (prefix is "" or "cp"))
                {
                    continue; // Dex, CP and appraisal bars are native simulator fields.
                }
            }
            else if (LooksNumeric().IsMatch(body) || body is "#" or "+" or "@")
            {
                return new(SearchCheckStatus.Invalid, [], term);
            }
            else if (!_keywords.Contains(body) && !TypeChart.Types.Contains(body, StringComparer.Ordinal)
                && !body.StartsWith('#') && !IsMoveType(body) && knownName?.Invoke(body) != true)
            {
                unverified ??= term;
            }

            // A player's status, move, name or tag is represented as a flag for the simulator's grammar.
            flags.Add(body);
        }

        var normalized = clauses.Select(clause => clause.Select(term => term.ToLowerInvariant()).ToArray());
        GameSearch.Parse(Join(normalized), flags);

        var parts = new List<string>();
        if (expression.Length <= CharacterLimit)
        {
            parts.Add(expression);
        }
        else if (clauses.Sum(clause => clause.Max(term => term.Length) + 1) - 1 > CharacterLimit || !Split(clauses, parts))
        {
            return new(SearchCheckStatus.CannotSplit, []);
        }

        return new(unverified is null ? SearchCheckStatus.Valid : SearchCheckStatus.Unverified, parts, unverified);
    }

    private static bool IsMoveType(string body)
    {
        if (!body.StartsWith('@'))
        {
            return false;
        }

        var start = body.Length > 1 && body[1] is '1' or '2' or '3' ? 2 : 1;
        return TypeChart.Types.Contains(body[start..], StringComparer.Ordinal);
    }

    private static bool ValidRange(Match term)
    {
        var prefix = term.Groups["prefix"].Value;
        var suffix = term.Groups["suffix"].Value;
        if (prefix.Length > 0 && suffix.Length > 0)
        {
            return false;
        }

        var first = term.Groups["first"].Value;
        var last = term.Groups["last"].Value;
        var range = term.Groups["dash"].Success;
        if (first.Length == 0 && last.Length == 0)
        {
            return false;
        }

        var limit = suffix.Length > 0 ? 4 : prefix switch
        {
            "buddy" => 5,
            "mega" or "maxmove" => 3,
            _ => int.MaxValue,
        };
        var minimum = prefix == "year" ? 2016 : prefix.Length == 0 && suffix.Length == 0 ? 1 : 0;
        var low = minimum;
        var high = limit;
        return (first.Length == 0 || (int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out low) && low >= minimum))
            && (last.Length == 0 || (int.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out high) && high <= limit))
            && low <= high && (range || first.Length > 0);
    }

    // (A or B) and C can be pasted as A and C, then B and C. Every other clause stays in each part.
    private static bool Split(string[][] clauses, List<string> parts)
    {
        if (parts.Count >= MaxParts)
        {
            return false;
        }

        var search = Join(clauses);
        if (search.Length <= CharacterLimit)
        {
            parts.Add(search);
            return true;
        }

        var splitIndex = -1;
        var largest = 0;
        for (var i = 0; i < clauses.Length; i++)
        {
            var alternatives = clauses[i];
            var length = alternatives.Sum(term => term.Length + 1);
            if (alternatives.Length > 1 && length > largest)
            {
                splitIndex = i;
                largest = length;
            }
        }

        if (splitIndex < 0)
        {
            return false;
        }

        var terms = clauses[splitIndex];
        var midpoint = 1;
        var leftLength = terms[0].Length + 1;
        while (midpoint < terms.Length - 1 && (leftLength * 2) + terms[midpoint].Length + 1 < largest)
        {
            leftLength += terms[midpoint++].Length + 1;
        }

        var next = (string[][])clauses.Clone();
        next[splitIndex] = terms[..midpoint];
        if (!Split(next, parts))
        {
            return false;
        }

        next[splitIndex] = terms[midpoint..];
        return Split(next, parts);
    }

    private static string Join(IEnumerable<IEnumerable<string>> clauses) =>
        string.Join('&', clauses.Select(clause => string.Join(',', clause)));

    [GeneratedRegex(@"^(?<prefix>cp|hp|age|year|distance|buddy|mega|maxmove)?(?<first>[0-9]*)(?:(?<dash>-)(?<last>[0-9]*))?(?<suffix>attack|defense|hp|\*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberTerm();

    [GeneratedRegex(@"^(?:[0-9-]|(?:cp|hp|age|year|distance|buddy|mega|maxmove)\s*[0-9-])", RegexOptions.CultureInvariant)]
    private static partial Regex LooksNumeric();
}
