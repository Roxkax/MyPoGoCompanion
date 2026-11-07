using System.Globalization;
using System.Text.Json.Nodes;
using MyPoGoCompanion.Engine.Pvp;
using MyPoGoCompanion.Engine.Search;
using MyPoGoCompanion.GoldenFiles;

namespace MyPoGoCompanion.Engine.UnitTests;

public sealed class SearchTests
{
    private static readonly GoldenDirectory _golden = GoldenDirectory.KeeperFilters;
    private static readonly HashSet<string> _noFlags = [];

    /// <summary>
    /// Every expression of the Python build's simulator cases (tests/golden/keeper-filters/outputs/searchsim.json),
    /// evaluated on the same small universe, lists the same matches in the same order, and the same expressions are
    /// refused.
    /// </summary>
    [Fact]
    public void Simulator_MatchesThePythonBuild()
    {
        var expected = _golden.ReadOutput("searchsim");
        var universe = expected["universe"]!;
        var dexes = universe["dex"]!.AsArray().Select(d => (int)d!).ToList();
        var bars = universe["bars"]!.AsArray().Select(b => b!.AsArray().Select(v => (int)v!).ToArray()).ToList();
        var cps = universe["cp"]!.AsArray().Select(c => (int)c!).ToList();
        var flagged = universe["flags"]!.AsObject().ToDictionary(f => f.Key, f => f.Value!.AsArray().Select(d => (int)d!).ToHashSet());
        var keywords = flagged.Keys.ToHashSet(StringComparer.Ordinal);

        var cases = new JsonArray();
        foreach (var expression in expected["cases"]!.AsArray().Select(c => (string)c!["expression"]!))
        {
            var search = GameSearch.Parse(expression, keywords);
            var matches = new JsonArray();
            foreach (var dex in dexes)
            {
                var flags = flagged.Where(f => f.Value.Contains(dex)).Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
                foreach (var bar in bars)
                {
                    foreach (var cp in cps)
                    {
                        if (search.Matches(new SearchSubject(dex, bar[0], bar[1], bar[2], cp, flags)))
                        {
                            matches.Add(new JsonArray(dex, bar[0], bar[1], bar[2], cp));
                        }
                    }
                }
            }

            cases.Add(new JsonObject { ["expression"] = expression, ["matchCount"] = matches.Count, ["matches"] = matches });
        }

        var unsupported = new JsonArray();
        foreach (var expression in expected["unsupported"]!.AsArray().Select(e => (string)e!))
        {
            Assert.Throws<UnsupportedSearchException>(() => GameSearch.Parse(expression, keywords));
            unsupported.Add(expression);
        }

        var actual = new JsonObject
        {
            ["universe"] = universe.DeepClone(),
            ["cases"] = cases,
            ["unsupported"] = unsupported,
        };
        GoldenAssert.Matches(_golden, "searchsim", actual);
        Assert.Equal(36, cases.Count);
    }

    [Theory]
    [InlineData("147", 147, 0, 0, 0, true)]
    [InlineData("!!147", 147, 0, 0, 0, false)] // as searchsim.py: the second "!" is ignored
    [InlineData("-", 1, 0, 0, 0, true)]
    [InlineData("4attack&4defense|4hp", 1, 4, 4, 4, true)]
    [InlineData("4attack&4defense|4hp", 1, 4, 4, 3, false)]
    [InlineData("1-3,3-4attack", 9, 3, 0, 0, true)]
    [InlineData("1-3,3-4attack", 9, 2, 0, 0, false)]
    [InlineData("99999999999", 1, 0, 0, 0, false)]
    public void Matches_Terms(string expression, int dex, int attack, int defense, int hp, bool expected) =>
        Assert.Equal(expected, GameSearch.Parse(expression).Matches(new SearchSubject(dex, attack, defense, hp)));

    [Fact]
    public void Parse_KeepsTheExpression_AndKnowsOnlyTheFlagsItIsGiven()
    {
        Assert.Equal("147&4attack", GameSearch.Parse("147&4attack").Expression);
        Assert.Throws<UnsupportedSearchException>(() => GameSearch.Parse("maxmove1-"));
        var search = GameSearch.Parse("!maxmove1-", new HashSet<string>(StringComparer.Ordinal) { "maxmove1-" });
        Assert.True(search.Matches(new SearchSubject(1, 0, 0, 0)));
        Assert.False(search.Matches(new SearchSubject(1, 0, 0, 0, Flags: new HashSet<string> { "maxmove1-" })));
    }

    [Theory]
    [InlineData("", "Unsupported search term \"\" in \"\".")]
    [InlineData("4*", "Star ratings (\"4*\") are not simulated, in \"4*\".")]
    [InlineData("147&shiny", "Unsupported search term \"shiny\" in \"147&shiny\".")]
    public void Parse_RefusesWhatItDoesntKnow(string expression, string message) =>
        Assert.Equal(message, Assert.Throws<UnsupportedSearchException>(() => GameSearch.Parse(expression, _noFlags)).Message);

    [Fact]
    public void Parse_ShortensLongExpressionsInItsMessage()
    {
        var message = Assert.Throws<UnsupportedSearchException>(() => GameSearch.Parse(new string('1', 70) + "&x")).Message;
        Assert.Equal($"Unsupported search term \"x\" in \"{new string('1', 60)}…\".", message);
    }

    [Fact]
    public void Matches_ACpTermNeedsTheCp() =>
        Assert.Throws<InvalidOperationException>(() => GameSearch.Parse("cp-1500").Matches(new SearchSubject(1, 0, 0, 0)));

    /// <summary>The Python build's own strings pass against its own boxes, as its build checked.</summary>
    [Theory]
    [InlineData("GL", 1500, 2)]
    [InlineData("UL", 2500, 3)]
    public void KeeperStrings_ThePythonBuildsStringsPass(string league, int cap, int parts)
    {
        var (boxes, strings) = Load(league);
        Assert.Equal(parts, strings.Count);
        var result = KeeperStringCheck.Check(boxes, strings, cap);
        Assert.True(result.Passed, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal(KeeperStringCheck.LastDexChecked * 125 * 2, result.Cases);
    }

    [Fact]
    public void KeeperStrings_ABoxTheStringsDontMatch_IsDescribed()
    {
        var (boxes, strings) = Load("GL");
        var changed = new Dictionary<int, KeeperBox>(boxes)
        {
            [149] = new(new BarRange(4, 4), BarRange.Any, BarRange.Any), // Dragonite: Attack 4 only
        };
        var result = KeeperStringCheck.Check(changed, strings, Leagues.Great);
        Assert.False(result.Passed);
        Assert.Equal(KeeperStringCheck.ProblemsShown + 1, result.Problems.Count);
        Assert.StartsWith("Pokédex #149 with appraisal bars ", result.Problems[0], StringComparison.Ordinal);
        Assert.Matches(@"^… and [0-9,]+ more\.$", result.Problems[^1]);
    }

    [Fact]
    public void KeeperStrings_MatchingTwiceOrOverTheCap_IsWrong()
    {
        var boxes = new Dictionary<int, KeeperBox> { [1] = new(BarRange.Any, BarRange.Any, BarRange.Any) };
        var twice = KeeperStringCheck.Check(boxes, ["1&cp-1500", "1&cp-1500"], Leagues.Great);
        Assert.Equal(KeeperStringCheck.ProblemsShown + 1, twice.Problems.Count);
        Assert.Equal("Pokédex #1 with appraisal bars 0/0/0 at CP 1,500 (under the cap) matched 2 strings, expected 1 string.", twice.Problems[0]);

        var overCap = KeeperStringCheck.Check(boxes, ["1"], Leagues.Great);
        Assert.Equal("Pokédex #1 with appraisal bars 0/0/0 at CP 1,501 (over the cap) matched 1 string, expected no string.", overCap.Problems[0]);

        var right = KeeperStringCheck.Check(boxes, ["1&cp-1500"], Leagues.Great);
        Assert.True(right.Passed);
    }

    [Fact]
    public void KeeperStrings_TooLongOrUnsupported_AreRefusedBeforeSimulating()
    {
        var result = KeeperStringCheck.Check(new Dictionary<int, KeeperBox>(), [new string('1', 1951), "1&shiny"], Leagues.Great);
        Assert.Equal(0, result.Cases);
        Assert.Equal(
            [
                "Part 1 is 1,951 characters long (at most 1,950).",
                "Part 2: Unsupported search term \"shiny\" in \"1&shiny\".",
            ],
            result.Problems);
    }

    private static (Dictionary<int, KeeperBox> Boxes, List<string> Strings) Load(string league)
    {
        var boxes = _golden.ReadOutput("pvp_boxes")["leagues"]![league]!["boxes"]!.AsObject().ToDictionary(
            pair => int.Parse(pair.Key, CultureInfo.InvariantCulture),
            pair =>
            {
                var box = pair.Value!["box"]!.AsArray();
                return new KeeperBox(Range(box[0]!), Range(box[1]!), Range(box[2]!));
            });
        var strings = _golden.ReadOutput("pvp_strings")[league]!["strings"]!.AsArray().Select(s => (string)s!["text"]!).ToList();
        return (boxes, strings);
    }

    private static BarRange Range(JsonNode range) => new((int)range[0]!, (int)range[1]!);
}
