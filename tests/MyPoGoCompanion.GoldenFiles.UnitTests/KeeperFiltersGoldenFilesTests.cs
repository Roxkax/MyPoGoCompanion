using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyPoGoCompanion.GoldenFiles.UnitTests;

/// <summary>
/// The frozen keeper-filters outputs are complete, unchanged and consistent, so the C# port is
/// compared with something trustworthy. Refresh them with legacy/keeper-filters/scripts/golden.py.
/// </summary>
public partial class KeeperFiltersGoldenFilesTests
{
    private const int MaxDex = 1025;
    private const int MaxLength = 1950;
    private static readonly GoldenDirectory _golden = GoldenDirectory.KeeperFilters;
    private static readonly string[] _leagues = ["GL", "UL"];

    public static TheoryData<string> Outputs =>
        ["pvp_boxes", "pvp_strings", "raiders", "data_pvp", "data_raid", "data_mega", "searchsim"];

    [Fact]
    public void Manifest_EveryFileIsAsFrozen() => Assert.Empty(_golden.CheckManifest());

    [Fact]
    public void Manifest_NamesTheSourceCommits()
    {
        var sources = _golden.ReadManifest()["sources"]!;

        foreach (var name in new[] { "pvpoke", "gm" })
        {
            Assert.Matches("^[0-9a-f]{40}$", (string)sources[name]!["commit"]!);
            Assert.StartsWith("https://github.com/", (string)sources[name]!["url"]!);
        }
    }

    [Theory]
    [MemberData(nameof(Outputs))]
    public void Output_MatchesItself(string name) =>
        GoldenAssert.Matches(_golden, name, _golden.ReadOutput(name));

    [Fact]
    public void Inputs_AreLaidOutLikeThePythonDataFolder()
    {
        foreach (var path in new[]
                 {
                     "pvpoke/src/data/gamemaster.json", "pvpoke/src/js/pokemon/Pokemon.js",
                     "pvpoke/src/data/rankings/all/overall/rankings-1500.json",
                     "pvpoke/src/data/rankings/all/overall/rankings-2500.json", "gm/GAME_MASTER.json",
                 })
        {
            Assert.True(File.Exists(_golden.Input(path)), path);
        }
    }

    [Fact]
    public void Inputs_GameMasterKeepsOnlyMoveAndPokemonTemplates()
    {
        var templates = JsonNode.Parse(File.ReadAllText(_golden.Input("gm/GAME_MASTER.json")))!.AsArray();

        Assert.NotEmpty(templates);
        Assert.All(templates, t => Assert.Matches(@"^V\d{4}_(MOVE|POKEMON)_", (string)t!["templateId"]!));
    }

    [Theory]
    [InlineData("GL", 1500)]
    [InlineData("UL", 2500)]
    public void PvpStrings_CoverEveryPokedexNumberOnceAndFitTheLimit(string league, int cap)
    {
        var data = _golden.ReadOutput("pvp_strings")[league]!;
        var next = 1;

        Assert.Equal(cap, (int)data["cap"]!);
        foreach (var row in data["strings"]!.AsArray())
        {
            var text = (string)row!["text"]!;
            Assert.Equal(next, (int)row["from"]!);
            Assert.Equal(text.Length, (int)row["length"]!);
            Assert.InRange(text.Length, 1, MaxLength);
            Assert.EndsWith($"&cp-{cap}", text);
            next = (int)row["to"]! + 1;
        }

        Assert.Equal(MaxDex + 1, next);
    }

    [Fact]
    public void PvpBoxes_EveryBoxHoldsTheRankOneSpreadsItWasBuiltFrom()
    {
        var leagues = _golden.ReadOutput("pvp_boxes")["leagues"]!;

        foreach (var league in _leagues)
        {
            var boxes = leagues[league]!["boxes"]!.AsObject();
            Assert.Equal(Enumerable.Range(1, MaxDex).Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                boxes.Select(b => b.Key));
            foreach (var (dex, entry) in boxes)
            {
                var box = entry!["box"]!.AsArray().Select(r => ((int)r![0]!, (int)r[1]!)).ToArray();
                Assert.Equal(3, box.Length);
                Assert.All(box, r => Assert.True(r.Item1 >= 0 && r.Item1 <= r.Item2 && r.Item2 <= 4, $"{league} #{dex}"));
                foreach (var (_, rank1) in entry["rank1"]!.AsObject())
                {
                    var ivs = rank1!["iv"]!.AsArray().Select(iv => Bar((int)iv!)).ToArray();
                    Assert.True(ivs.Select((bar, stat) => bar >= box[stat].Item1 && bar <= box[stat].Item2).All(x => x),
                        $"{league} #{dex}: rank-1 bars {string.Join("/", ivs)} outside the box");
                }
            }
        }
    }

    [Fact]
    public void DataPvp_HasAFamilyRowForEveryPokedexNumber()
    {
        foreach (var league in _leagues)
        {
            var rows = _golden.ReadOutput("data_pvp")["leagues"]![league]!["rows"]!.AsArray();
            var dexes = rows.SelectMany(r => r!["dex"]!.AsArray().Select(d => (int)d!)).Order();

            Assert.Equal(Enumerable.Range(1, MaxDex), dexes);
            Assert.All(rows, r => Assert.InRange(((string)r!["string"]!).Length, 1, MaxLength));
        }
    }

    [Fact]
    public void Raiders_EveryTypeIsRankedByScore()
    {
        var types = _golden.ReadOutput("raiders")["types"]!.AsObject();

        Assert.Equal(18, types.Count);
        foreach (var (type, data) in types)
        {
            var scores = data!["ranked"]!.AsArray().Select(r => (double)r!["er"]!).ToArray();
            Assert.NotEmpty(scores);
            Assert.True(scores.Zip(scores.Skip(1)).All(p => p.First >= p.Second), type);
        }
    }

    [Fact]
    public void DataRaid_StringsHoldEveryPickAndFitTheLimit()
    {
        var raid = _golden.ReadOutput("data_raid");
        var strings = raid["raid"]!;

        Assert.Equal((int)raid["dexCount"]!, (int)raid["attackerCount"]! + (int)raid["preEvoCount"]!);
        Assert.EndsWith("&3-4attack", (string)strings["11-15"]!);
        Assert.EndsWith("&4attack", (string)strings["15"]!);
        Assert.All(new[] { (string)strings["11-15"]!, (string)strings["15"]! }, s => Assert.InRange(s.Length, 1, MaxLength));
        Assert.Equal((int)raid["dexCount"]!, CountDexNumbers(((string)strings["15"]!).Split('&')[0]));
        Assert.Equal("maxmove1-&3-4attack", (string)raid["max"]!["11-15"]!);
    }

    [Fact]
    public void DataMega_StringsStartWithEveryMegaInGo()
    {
        var mega = _golden.ReadOutput("data_mega");

        Assert.StartsWith("mega0-,", (string)mega["string"]!);
        Assert.StartsWith((string)mega["string"]! + ",", (string)mega["stringWithPre"]!);
        Assert.InRange(((string)mega["stringWithPre"]!).Length, 1, MaxLength);
        Assert.NotEmpty(mega["released"]!.AsArray());
    }

    [Fact]
    public void SearchSim_CasesStayInsideTheirUniverse()
    {
        var sim = _golden.ReadOutput("searchsim");
        var universe = sim["universe"]!;
        var dex = universe["dex"]!.AsArray().Select(d => (int)d!).ToHashSet();
        var cp = universe["cp"]!.AsArray().Select(c => (int)c!).ToHashSet();
        var bars = universe["bars"]!.AsArray().Select(b => string.Join(",", b!.AsArray().Select(x => (int)x!))).ToHashSet();
        var size = dex.Count * cp.Count * bars.Count;

        Assert.NotEmpty(sim["unsupported"]!.AsArray());
        foreach (var c in sim["cases"]!.AsArray())
        {
            var matches = c!["matches"]!.AsArray().Select(m => m!.AsArray().Select(x => (int)x!).ToArray()).ToArray();
            Assert.Equal((int)c["matchCount"]!, matches.Length);
            Assert.InRange(matches.Length, 0, size);
            Assert.All(matches, m => Assert.True(dex.Contains(m[0]) && bars.Contains($"{m[1]},{m[2]},{m[3]}") && cp.Contains(m[4]),
                (string)c["expression"]!));
        }
    }

    private static int Bar(int iv) => iv switch
    {
        0 => 0,
        <= 5 => 1,
        <= 10 => 2,
        <= 14 => 3,
        _ => 4,
    };

    private static int CountDexNumbers(string ranges) =>
        DexRange().Matches(ranges).Sum(m =>
            m.Groups[2].Success ? int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)
                - int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) + 1 : 1);

    [GeneratedRegex(@"(\d+)(?:-(\d+))?")]
    private static partial Regex DexRange();
}
