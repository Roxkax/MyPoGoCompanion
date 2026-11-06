using System.Text.Json.Nodes;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.GoldenFiles;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>
/// The sanity checks on the fixtures' data (which pass, apart from the two rules the fixtures are too small for) and
/// on that data broken in each way a check looks for.
/// </summary>
public sealed class ImportChecksTests
{
    private static readonly DailyImportOptions _fixtureOptions = new() { FullPokedex = false, MinimumRaidRankingsPerType = 0 };

    private static ImportedGameData Data() => GameDataBuilder.Build(Fixtures.Sources());

    private static ImportCheck Check(ImportedGameData data, string name, int storedHighestDex = 0, DailyImportOptions? options = null) =>
        ImportChecks.Run(data, storedHighestDex, options ?? _fixtureOptions).Single(check => check.Name == name);

    [Fact]
    public void TheFixtures_PassEveryCheck()
    {
        var checks = ImportChecks.Run(Data(), 0, _fixtureOptions);

        Assert.Equal(["Pokédex", "Numbers", "PvP rankings", "Best IVs", "Keeper boxes", "Raid rankings", "Raid movesets", "Keeper strings"], checks.Select(c => c.Name));
        Assert.All(checks, check => Assert.True(check.Passed, string.Join(" ", check.Problems)));
        Assert.Equal("no strings to check yet (they come with the keeper filters, E04-F01)", checks[^1].Detail);
        Assert.StartsWith("28 Pokédex numbers up to #", checks[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaults_WantTheWholePokedex_AndTenRaidersPerType()
    {
        var checks = ImportChecks.Run(Data(), 0, new DailyImportOptions());

        Assert.Contains("Pokédex numbers missing between #1 and #", Check(Data(), "Pokédex", options: new DailyImportOptions()).Problems[0], StringComparison.Ordinal);
        Assert.Contains(checks.Single(c => c.Name == "Raid rankings").Problems, p => p.StartsWith("Fighting has 0 entries, at least 10 expected.", StringComparison.Ordinal));
        Assert.Matches(@"^… and [0-9]+ more\.$", checks.Single(c => c.Name == "Raid rankings").Problems[^1]);
    }

    [Fact]
    public void Pokedex_AFallingHighestNumber_AndAMissingMainForm()
    {
        var data = Data();
        var charizard = data.Species.Single(s => s.Dex == 6);
        data.Forms.Single(f => f.Id == "charizard").IsDefault = false;

        var check = Check(data, "Pokédex", storedHighestDex: 2000);

        Assert.False(check.Passed);
        Assert.Contains(check.Problems, p => p.StartsWith("The highest Pokédex number is #", StringComparison.Ordinal) && p.EndsWith("lower than the #2000 already stored.", StringComparison.Ordinal));
        Assert.Contains($"{charizard.NameEn} (#6) has 0 main forms (exactly 1 expected).", check.Problems);
    }

    [Fact]
    public void Pokedex_NoPokemonAtAll()
    {
        var data = Data();
        data.Species.Clear();

        Assert.Contains("The sources list no Pokémon.", Check(data, "Pokédex").Problems);
    }

    [Fact]
    public void Numbers_StatsMaxCpAndCpMultipliers()
    {
        var data = Data();
        data.Forms.Single(f => f.Id == "charizard").Defense = 0;
        data.Forms.Single(f => f.Id == "bulbasaur").MaxCp50 = 10_001;
        var level20 = data.CpMultipliers.Single(m => m.Level == 20m);
        level20.Value = 0.1;
        data.CpMultipliers.RemoveAll(m => m.Level == 51m);

        var check = Check(data, "Numbers");

        Assert.Contains(check.Problems, p => p.StartsWith("Charizard (charizard) has base stats ", StringComparison.Ordinal));
        Assert.Contains("Bulbasaur (bulbasaur) has a max CP of 10,001 at level 50 (10–10,000 expected).", check.Problems);
        Assert.Contains("The CP multipliers cover 100 levels; every level from 1 to 51 in steps of 0.5 (101) is expected.", check.Problems);
        Assert.Contains(check.Problems, p => p.StartsWith("The CP multiplier of level 20 (0.1) isn't higher than level 19.5's", StringComparison.Ordinal));
    }

    [Fact]
    public void PvpRankings_ARepeatedRank_OrNoRanking()
    {
        var data = Data();
        var great = data.PvpRankings.Where(r => r.League == League.Great).ToList();
        great[1].Rank = great[0].Rank;
        data.PvpRankings.RemoveAll(r => r.League == League.Master);

        var check = Check(data, "PvP rankings");

        Assert.Contains($"Great League: rank {great[0].Rank} is used 2 times (each rank once, from 1, expected).", check.Problems);
        Assert.Contains("Master League has no ranking.", check.Problems);
    }

    [Fact]
    public void BestIvs_OverTheCap_OrMissingForAMainForm()
    {
        var data = Data();
        var best = data.PvpBestIvs.First(b => b.League == League.Great);
        best.Cp = 1501;
        data.PvpBestIvs.RemoveAll(b => b.League == League.Ultra && b.FormId == "charizard");

        var check = Check(data, "Best IVs");

        Assert.Contains(check.Problems, p => p.StartsWith($"Great League: {best.FormId}'s best IVs ", StringComparison.Ordinal) && p.Contains("give CP 1,501 (at most 1,500, level 1–51).", StringComparison.Ordinal));
        Assert.Contains("Ultra League: Charizard (#6) has no best IVs.", check.Problems);
    }

    [Fact]
    public void KeeperBoxes_MissingOrInvalid()
    {
        var data = Data();
        data.PvpKeeperBoxes.RemoveAll(b => b.League == League.Great && b.Dex == 6);
        data.PvpKeeperBoxes.First(b => b.League == League.Ultra).AttackBarMax = 5;

        var check = Check(data, "Keeper boxes");

        Assert.Contains("Great League: 1 Pokédex number without a keeper box: #6.", check.Problems);
        Assert.Contains(check.Problems, p => p.StartsWith("Ultra League: #", StringComparison.Ordinal) && p.EndsWith("isn't a valid box.", StringComparison.Ordinal));
    }

    [Fact]
    public void RaidRankings_GapsAndImpossibleNumbers()
    {
        var data = Data();
        var fire = data.RaidRankings.Where(r => r.AttackingTypeId == "fire").OrderBy(r => r.Rank).ToList();
        fire[0].Rank = 99;
        fire[1].Dps = double.NaN;

        var check = Check(data, "Raid rankings");

        Assert.Contains(check.Problems, p => p.StartsWith($"Fire: the ranks aren't 1 to {fire.Count} without gaps or repeats", StringComparison.Ordinal));
        Assert.Contains(check.Problems, p => p.StartsWith($"Fire: {fire[1].FormId}", StringComparison.Ordinal) && p.Contains("has DPS NaN", StringComparison.Ordinal));
    }

    [Fact]
    public void RaidMovesets_GapsAndImpossibleNumbers()
    {
        var data = Data();
        var charizard = data.RaidMovesets.Where(m => m.FormId == "charizard" && !m.IsShadow).OrderBy(m => m.Rank).ToList();
        charizard[^1].Rank = charizard.Count + 5;
        charizard[0].Tdo = 0;

        var check = Check(data, "Raid movesets");

        Assert.Contains(check.Problems, p => p.StartsWith("charizard: the ranks aren't 1 to ", StringComparison.Ordinal));
        Assert.Contains(check.Problems, p => p.StartsWith($"charizard: {charizard[0].FastMoveId} + {charizard[0].ChargedMoveId} has DPS ", StringComparison.Ordinal));
    }

    [Fact]
    public void KeeperStrings_AreRunThroughTheSimulator()
    {
        // The Python build's strings and boxes: they pass, and a box that changed overnight fails them.
        var golden = GoldenDirectory.KeeperFilters;
        var data = new ImportedGameData();
        foreach (var (league, name) in new[] { (League.Great, "GL"), (League.Ultra, "UL") })
        {
            foreach (var (dex, box) in golden.ReadOutput("pvp_boxes")["leagues"]![name]!["boxes"]!.AsObject())
            {
                var bars = box!["box"]!.AsArray();
                data.PvpKeeperBoxes.Add(new PvpKeeperBox
                {
                    League = league,
                    Dex = int.Parse(dex, System.Globalization.CultureInfo.InvariantCulture),
                    AttackBarMin = Bar(bars, 0, 0),
                    AttackBarMax = Bar(bars, 0, 1),
                    DefenseBarMin = Bar(bars, 1, 0),
                    DefenseBarMax = Bar(bars, 1, 1),
                    StaminaBarMin = Bar(bars, 2, 0),
                    StaminaBarMax = Bar(bars, 2, 1),
                    JudgedBy = ["x"],
                });
            }

            var part = 0;
            foreach (var text in golden.ReadOutput("pvp_strings")[name]!["strings"]!.AsArray().Select(s => (string)s!["text"]!))
            {
                data.KeeperFilters.Add(new KeeperFilter
                {
                    Kind = league == League.Great ? KeeperFilterKind.GreatLeague : KeeperFilterKind.UltraLeague,
                    Part = ++part,
                    SearchString = text,
                });
            }
        }

        var passed = Check(data, "Keeper strings");
        Assert.True(passed.Passed, string.Join(" ", passed.Problems));
        Assert.Equal("Great League: 2 parts, 275,000 cases; Ultra League: 3 parts, 275,000 cases", passed.Detail);

        data.PvpKeeperBoxes.Single(b => b.League == League.Great && b.Dex == 149).AttackBarMin = 4;
        var failed = Check(data, "Keeper strings");
        Assert.False(failed.Passed);
        Assert.StartsWith("Great League: Pokédex #149 with appraisal bars ", failed.Problems[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Explain_ListsEveryFailedCheck()
    {
        ImportCheck[] checks =
        [
            new("Pokédex", true, []),
            new("Numbers", false, ["A.", "B."]),
            new("Raid rankings", false, ["C."]),
        ];

        Assert.Equal(
            "The new data failed 2 sanity checks, so nothing was published (yesterday's data stays live). Numbers: A. B. Raid rankings: C.",
            ImportChecks.Explain(checks));
        Assert.StartsWith("The new data failed 1 sanity check,", ImportChecks.Explain(checks[..2]), StringComparison.Ordinal);
    }

    private static int Bar(JsonArray bars, int stat, int end) => (int)bars[stat]![end]!;
}
