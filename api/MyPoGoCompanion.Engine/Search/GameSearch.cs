using System.Globalization;
using System.Text.RegularExpressions;

namespace MyPoGoCompanion.Engine.Search;

/// <summary>A search term the simulator doesn't know (the game may; the strings the app publishes never use it).</summary>
public sealed class UnsupportedSearchException(string message) : ArgumentException(message);

/// <summary>
/// One Pokémon as the game's storage search sees it: Pokédex number, appraisal bars (0–4), CP and the keyword
/// flags it matches (e.g. <c>maxmove1-</c>, <c>mega0-</c>).
/// </summary>
public readonly record struct SearchSubject(int Dex, int AttackBar, int DefenseBar, int HpBar, int? Cp = null, IReadOnlySet<string>? Flags = null);

/// <summary>
/// An independent simulator of Pokémon GO's storage search for the keywords the app's strings use (the Python
/// build's <c>searchsim.py</c>, checked against its golden cases): Pokédex numbers and ranges (<c>147</c>,
/// <c>147-149</c>, <c>-3</c>, <c>1025-</c>), appraisal bars (<c>3attack</c>, <c>3-4defense</c>, <c>0-1hp</c>),
/// CP (<c>cp-1500</c>, <c>cp1501-</c>) and keyword flags the caller names, each optionally negated with <c>!</c>.
/// <c>&amp;</c> and <c>|</c> both mean AND, <c>,</c> <c>;</c> <c>:</c> mean OR, and OR binds tighter (the game has no
/// brackets). Anything else (star ratings, <c>shiny</c>, names…) is refused rather than guessed.
/// </summary>
public sealed partial class GameSearch
{
    private enum Field
    {
        Dex,
        Cp,
        Attack,
        Defense,
        Hp,
        Flag,
    }

    private readonly record struct Term(Field Field, int? Min, int? Max, bool Negated, string? Flag)
    {
        public bool Matches(in SearchSubject subject)
        {
            bool hit;
            if (Field == Field.Flag)
            {
                hit = subject.Flags?.Contains(Flag!) == true;
            }
            else
            {
                var value = Field switch
                {
                    Field.Dex => subject.Dex,
                    Field.Attack => subject.AttackBar,
                    Field.Defense => subject.DefenseBar,
                    Field.Hp => subject.HpBar,
                    _ => subject.Cp ?? throw new InvalidOperationException("A cp term needs the Pokémon's CP."),
                };
                hit = (Min is not { } min || value >= min) && (Max is not { } max || value <= max);
            }

            return hit != Negated;
        }
    }

    // As searchsim.py: optional "!", optional "cp", then a number or a range, then an optional stat keyword.
    [GeneratedRegex(@"^(!?)(cp)?(?:([0-9]*)-([0-9]*)|([0-9]+))(attack|defense|hp|\*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TermPattern();

    private static readonly char[] _and = ['&', '|'];
    private static readonly char[] _or = [',', ';', ':'];

    private readonly Term[][] _clauses;

    private GameSearch(string expression, Term[][] clauses)
    {
        Expression = expression;
        _clauses = clauses;
    }

    public string Expression { get; }

    /// <summary>Reads a search string.</summary>
    /// <param name="expression">The string, as typed into the game.</param>
    /// <param name="flagKeywords">Keywords the caller answers per Pokémon (<see cref="SearchSubject.Flags"/>).</param>
    /// <exception cref="UnsupportedSearchException">A term the simulator doesn't know.</exception>
    public static GameSearch Parse(string expression, IReadOnlySet<string>? flagKeywords = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var clauses = expression.Split(_and)
            .Select(clause => clause.Split(_or).Select(term => ParseTerm(term.Trim(), expression, flagKeywords)).ToArray())
            .ToArray();
        return new GameSearch(expression, clauses);
    }

    /// <summary>Whether the game would show this Pokémon for the string.</summary>
    /// <exception cref="InvalidOperationException">The string has a cp term and the subject no CP.</exception>
    public bool Matches(in SearchSubject subject)
    {
        foreach (var clause in _clauses)
        {
            var any = false;
            foreach (var term in clause)
            {
                if (term.Matches(subject))
                {
                    any = true;
                    break;
                }
            }

            if (!any)
            {
                return false;
            }
        }

        return true;
    }

    private static Term ParseTerm(string term, string expression, IReadOnlySet<string>? flagKeywords)
    {
        var negated = term.StartsWith('!');
        var body = negated ? term[1..] : term;
        if (flagKeywords?.Contains(body) == true)
        {
            return new Term(Field.Flag, null, null, negated, body);
        }

        var match = TermPattern().Match(body);
        if (!match.Success)
        {
            throw new UnsupportedSearchException($"Unsupported search term \"{term}\" in \"{Shorten(expression)}\".");
        }

        var keyword = match.Groups[6].Success ? match.Groups[6].Value : match.Groups[2].Success ? "cp" : null;
        if (keyword == "*")
        {
            throw new UnsupportedSearchException($"Star ratings (\"{term}\") are not simulated, in \"{Shorten(expression)}\".");
        }

        int? min, max;
        if (match.Groups[5].Success)
        {
            min = max = Number(match.Groups[5].Value);
        }
        else
        {
            min = match.Groups[3].Length > 0 ? Number(match.Groups[3].Value) : null;
            max = match.Groups[4].Length > 0 ? Number(match.Groups[4].Value) : null;
        }

        var field = keyword switch
        {
            null => Field.Dex,
            "cp" => Field.Cp,
            "attack" => Field.Attack,
            "defense" => Field.Defense,
            _ => Field.Hp,
        };
        return new Term(field, min, max, negated, null);
    }

    private static int Number(string digits) =>
        int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : int.MaxValue;

    private static string Shorten(string expression) => expression.Length <= 60 ? expression : expression[..60] + "…";
}
