using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using MyPoGoCompanion.Engine;
using MyPoGoCompanion.Engine.Pvp;
using MyPoGoCompanion.Engine.Raids;
using MyPoGoCompanion.GoldenFiles;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>
/// The C# engine against the Python keeper build's golden files (tests/golden/keeper-filters): the frozen inputs go
/// through the import's own readers and <see cref="EngineInputs"/>, and the results, written in the Python files'
/// shape, must match exactly.
/// </summary>
public sealed class KeeperGoldenFilesTests
{
    private static readonly GoldenDirectory _golden = GoldenDirectory.KeeperFilters;
    private static readonly Lazy<(EngineInputs Inputs, List<PvpokeRanking> Great, List<PvpokeRanking> Ultra)> _inputs = new(Load);

    [Fact]
    public void PvpBoxesMatchThePythonBuild()
    {
        var (inputs, great, ultra) = _inputs.Value;
        var calculator = new KeeperCalculator(inputs.Entries, inputs.Multipliers);
        var leagues = new JsonObject();
        foreach (var (name, cap, ranking) in new[] { ("GL", Leagues.Great, great), ("UL", Leagues.Ultra, ultra) })
        {
            var boxes = new JsonObject();
            foreach (var box in calculator.Boxes(cap, ranking.Select(r => r.SpeciesId)))
            {
                var rank1 = new JsonObject();
                foreach (var one in box.RankOnes)
                {
                    rank1[one.FormId] = new JsonObject
                    {
                        ["iv"] = new JsonArray(one.Ivs.Attack, one.Ivs.Defense, one.Ivs.Stamina),
                        ["level"] = (double)one.Level,
                        ["cp"] = one.Cp,
                    };
                }

                var fit = box.Fit;
                boxes[box.Dex.ToString(CultureInfo.InvariantCulture)] = new JsonObject
                {
                    ["box"] = new JsonArray(Range(fit.Box.Attack), Range(fit.Box.Defense), Range(fit.Box.Stamina)),
                    ["f1"] = PythonRound(fit.F1, 4),
                    ["tp"] = fit.TruePositives,
                    ["inBox"] = fit.InBox,
                    ["keepers"] = fit.Keepers,
                    ["sources"] = Strings(box.JudgedBy),
                    ["rank1"] = rank1,
                    ["forms"] = Strings(box.Forms),
                };
            }

            leagues[name] = new JsonObject { ["cap"] = cap, ["boxes"] = boxes };
        }

        var actual = new JsonObject
        {
            ["policy"] = "meta",
            ["keep"] = KeeperBoxes.KeepShare,
            ["maxLevel"] = (double)Leagues.MaxLevel,
            ["leagues"] = leagues,
        };
        GoldenAssert.Matches(_golden, "pvp_boxes", actual);
    }

    [Fact]
    public void RaidRankingsMatchThePythonBuild()
    {
        var (inputs, _, _) = _inputs.Value;
        var ranker = new RaidRanker(inputs.Entries, inputs.Moves, inputs.Multipliers);
        var rankings = ranker.RankAll();
        var types = new JsonObject();
        foreach (var type in rankings)
        {
            types[type.Type] = new JsonObject
            {
                ["tenth"] = type.Tenth,
                ["ranked"] = new JsonArray([.. type.Ranked.Select(Entry)]),
            };
        }

        var (attackers, pre) = RaidRanker.Selection(rankings);
        var actual = new JsonObject
        {
            ["level"] = (double)RaidFormula.Level,
            ["targetDef"] = RaidFormula.NeutralDefense,
            ["types"] = types,
            ["attackers"] = new JsonArray([.. attackers.Select(d => (JsonNode)d)]),
            ["preEvos"] = new JsonArray([.. pre.Select(d => (JsonNode)d)]),
        };
        GoldenAssert.Matches(_golden, "raiders", actual);
    }

    [Theory]
    [InlineData(0.38715, 4, 0.3871)] // 0.38715 is just below 0.38715 in binary
    [InlineData(0.03125, 4, 0.0312)] // an exact tie rounds to even, as Python does
    [InlineData(0.09375, 4, 0.0938)]
    [InlineData(2.675, 2, 2.67)] // 2.675 is just below 2.675 in binary
    [InlineData(-1.0, 4, -1.0)]
    [InlineData(1.0, 4, 1.0)]
    public void PythonRoundRoundsLikePython(double value, int digits, double expected) =>
        Assert.Equal(expected, PythonRound(value, digits));

    private static JsonObject Entry(RaidEntryScore r)
    {
        var s = r.Best;
        var d = s.Detail;
        return new JsonObject
        {
            ["speciesId"] = r.Pokemon.Id,
            ["name"] = r.Pokemon.Name,
            ["dex"] = r.Pokemon.Dex,
            ["mega"] = r.Pokemon.IsMega,
            ["shadow"] = r.Pokemon.IsShadow,
            ["types"] = Strings(r.Pokemon.Types),
            ["eliteFast"] = r.EliteFast,
            ["eliteCharged"] = r.EliteCharged,
            ["preEvos"] = new JsonArray([.. r.PreEvolutions.Select(x => (JsonNode)x)]),
            ["fast"] = s.FastMoveId,
            ["charged"] = s.ChargedMoveId,
            ["dps"] = s.Dps,
            ["tdo"] = s.Tdo,
            ["er"] = s.Score,
            ["detail"] = new JsonObject
            {
                ["atk"] = d.Stats.Attack,
                ["def"] = d.Stats.Defense,
                ["hp"] = d.Stats.Hp,
                ["y"] = d.IncomingDps,
                ["fastMult"] = d.FastMultiplier,
                ["chargedMult"] = d.ChargedMultiplier,
                ["fastPower"] = d.FastPower,
                ["fastDur"] = d.FastDuration,
                ["fastEnergy"] = d.FastEnergy,
                ["fastDmg"] = d.FastDamage,
                ["chargedPower"] = d.ChargedPower,
                ["chargedDur"] = d.ChargedDuration,
                ["chargedCost"] = d.ChargedCost,
                ["chargedDws"] = d.ChargedDamageWindow,
                ["ce"] = d.ChargedEnergy,
                ["fdps"] = d.FastDps,
                ["feps"] = d.FastEps,
                ["cdps"] = d.ChargedDps,
                ["ceps"] = d.ChargedEps,
                ["x"] = d.EnergyFromDamage,
                ["dps0"] = d.BaseDps,
            },
            ["top"] = r.Top,
            ["megaPick"] = r.MegaPick,
        };
    }

    private static (EngineInputs, List<PvpokeRanking>, List<PvpokeRanking>) Load()
    {
        SourceFile File(string path) => new(path, new Uri("https://example.test/" + path), System.IO.File.ReadAllBytes(_golden.Input(path)));
        var pvpoke = PvpokeSource.ReadGameMaster(File("pvpoke/src/data/gamemaster.json"));
        var gameMaster = GameMasterSource.Read(File("gm/GAME_MASTER.json"));
        var multipliers = PvpokeSource.ReadCpMultipliers(File("pvpoke/src/js/pokemon/Pokemon.js"), GameDataBuilder.MaxLevel);
        return (EngineInputs.From(pvpoke, gameMaster, multipliers),
            PvpokeSource.ReadRankings(File("pvpoke/src/data/rankings/all/overall/rankings-1500.json")),
            PvpokeSource.ReadRankings(File("pvpoke/src/data/rankings/all/overall/rankings-2500.json")));
    }

    private static JsonArray Range(BarRange range) => new(range.Min, range.Max);

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(v => (JsonNode)v)]);

    /// <summary>
    /// Python's <c>round(x, n)</c>: the exact binary value rounded to n decimals, an exact tie to the even digit,
    /// then read back as the nearest double.
    /// </summary>
    internal static double PythonRound(double value, int digits)
    {
        var bits = BitConverter.DoubleToInt64Bits(Math.Abs(value));
        var exponent = (int)((bits >> 52) & 0x7FF);
        var mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantissa |= 1L << 52;
        }

        // |value| = mantissa × 2^(exponent − 1075); times 10^digits as numerator / denominator.
        var shift = exponent - 1075;
        var numerator = new BigInteger(mantissa) * BigInteger.Pow(10, digits);
        var denominator = BigInteger.One;
        if (shift >= 0)
        {
            numerator <<= shift;
        }
        else
        {
            denominator <<= -shift;
        }

        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        var twice = remainder * 2;
        if (twice > denominator || (twice == denominator && !quotient.IsEven))
        {
            quotient += 1;
        }

        var text = (value < 0 ? "-" : "") + quotient.ToString(CultureInfo.InvariantCulture) + "e-" + digits.ToString(CultureInfo.InvariantCulture);
        return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
