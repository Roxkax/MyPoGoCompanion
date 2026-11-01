using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Data.Seed;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.UnitTests;

/// <summary>The development database's sample data hangs together and follows the clock.</summary>
public class DevSeedTests
{
    private static readonly DateTimeOffset _sampleMoment = new(DevSeed.SampleNow, TimeSpan.Zero);

    private static readonly DevSeedData _sample = DevSeed.Build(_sampleMoment);

    [Fact]
    public void GameData_IsTheSampleSlice()
    {
        Assert.Equal(18, _sample.Types.Count);
        Assert.Equal(91, _sample.Species.Count);
        Assert.Equal(_sample.Species.Count, _sample.Forms.Count(form => form.IsDefault));
        Assert.All(_sample.Species, species => Assert.Single(_sample.Forms, form => form.Dex == species.Dex && form.IsDefault));
        Assert.Equal(101, _sample.CpMultipliers.Count);
    }

    [Theory]
    [InlineData("garchomp", 261, 193, 239, 4479)]
    [InlineData("charizard_mega_y", 319, 212, 186, 5037)]
    public void Forms_HaveTheDesignsNumbers(string formId, int attack, int defense, int stamina, int maxCp50)
    {
        var form = _sample.Forms.Single(form => form.Id == formId);

        Assert.Equal((attack, defense, stamina, maxCp50), (form.Attack, form.Defense, form.Stamina, form.MaxCp50));
    }

    [Fact]
    public void Forms_KnowTheirKind()
    {
        var kinds = _sample.Forms.ToDictionary(form => form.Id, form => form.Kind);

        Assert.Equal(FormKind.Base, kinds["charizard"]);
        Assert.Equal(FormKind.Mega, kinds["charizard_mega_x"]);
        Assert.Equal(FormKind.Primal, kinds["groudon_primal"]);
        Assert.Equal(FormKind.Regional, kinds["vulpix_alolan"]);
        Assert.Equal(FormKind.Alternate, kinds["landorus_therian"]);
        Assert.Equal("charizard", _sample.Forms.Single(form => form.Id == "charizard_mega_x").BaseFormId);
    }

    [Fact]
    public void References_PointAtSeededRows()
    {
        var types = _sample.Types.Select(type => type.Id).ToHashSet();
        var forms = _sample.Forms.Select(form => form.Id).ToHashSet();
        var moves = _sample.Moves.Select(move => move.Id).ToHashSet();
        var dex = _sample.Species.Select(species => species.Dex).ToHashSet();
        var families = _sample.Families.Select(family => family.Id).ToHashSet();

        Assert.All(_sample.Species, species => Assert.Contains(species.FamilyId, families));
        Assert.All(_sample.Forms, form =>
        {
            Assert.Contains(form.Dex, dex);
            Assert.Contains(form.Type1Id, types);
            Assert.True(form.Type2Id is null || types.Contains(form.Type2Id));
            Assert.True(form.BaseFormId is null || forms.Contains(form.BaseFormId));
        });
        Assert.All(_sample.Moves, move => Assert.Contains(move.TypeId, types));
        Assert.All(_sample.FormMoves, learn => Assert.True(forms.Contains(learn.FormId) && moves.Contains(learn.MoveId)));
        Assert.All(_sample.Evolutions, step => Assert.True(forms.Contains(step.FromFormId) && forms.Contains(step.ToFormId)));
        Assert.All(_sample.PvpRankings, row => Assert.True(forms.Contains(row.FormId) && moves.Contains(row.FastMoveId)));
        Assert.All(_sample.RaidRankings, row => Assert.True(forms.Contains(row.FormId) && types.Contains(row.AttackingTypeId)));
        Assert.All(_sample.PvpBestIvs, row => Assert.Contains(row.FormId, forms));
        Assert.All(_sample.PvpKeeperBoxes, row => Assert.Contains(row.Dex, dex));

        var liveForms = _sample.RaidBosses.Select(boss => boss.FormId)
            .Concat(_sample.MaxBosses.Select(boss => boss.FormId))
            .Concat(_sample.EggPoolEntries.Select(entry => entry.FormId))
            .Concat(_sample.ResearchTasks.SelectMany(task => task.Rewards).Select(reward => reward.FormId))
            .Concat(_sample.Events.SelectMany(gameEvent => gameEvent.Pokemon).Select(featured => featured.FormId))
            .Concat(_sample.CollectionPokemon.Select(pokemon => pokemon.FormId));
        Assert.All(liveForms, formId => Assert.Contains(formId, forms));
        Assert.All(_sample.CollectionPokemon, pokemon =>
            Assert.True(moves.Contains(pokemon.FastMoveId!) && moves.Contains(pokemon.ChargedMove1Id!)));
    }

    [Fact]
    public void KeeperFilters_AreTheGoldenStrings()
    {
        Assert.Equal(2, _sample.KeeperFilters.Count(filter => filter.Kind == KeeperFilterKind.GreatLeague));
        Assert.Equal(3, _sample.KeeperFilters.Count(filter => filter.Kind == KeeperFilterKind.UltraLeague));
        Assert.Equal("maxmove1-&3-4attack", _sample.KeeperFilters.Single(filter => filter.Kind == KeeperFilterKind.MaxBattle && filter.Variant.Length == 0).SearchString);
        Assert.All(_sample.KeeperFilters, filter => Assert.InRange(filter.SearchString.Length, 1, 1950));
    }

    [Fact]
    public void Raids_AtTheSampleMoment_AreTheDesignsRaids()
    {
        var now = DevSeed.SampleNow;
        var live = _sample.RaidBosses.Where(boss => boss.StartsAt <= now && (boss.EndsAt is null || boss.EndsAt > now))
            .Select(boss => (boss.FormId, boss.Tier, boss.IsShadow));

        Assert.Equal(
        [
            ("kyogre", RaidTier.FiveStar, false), ("gengar_mega", RaidTier.Mega, false), ("absol", RaidTier.ThreeStar, true),
            ("charizard", RaidTier.ThreeStar, false), ("lapras", RaidTier.ThreeStar, false), ("gible", RaidTier.OneStar, false),
            ("charmander", RaidTier.OneStar, false),
        ], live);
    }

    [Fact]
    public void Events_AtTheSampleMoment_HalloweenIsLiveAndCommunityDayIsNext()
    {
        var now = DevSeed.SampleNow;

        var live = Assert.Single(_sample.Events, gameEvent => gameEvent.StartsAt <= now && gameEvent.EndsAt > now);
        Assert.Equal("Halloween event", live.NameEn);
        var communityDay = Assert.Single(_sample.Events, gameEvent => gameEvent.Kind == EventKind.CommunityDay);
        Assert.Equal(new DateTime(2026, 11, 7, 14, 0, 0), communityDay.StartsAt);
        Assert.Equal(("garchomp", "EARTH_POWER"), (communityDay.Moves.Single().FormId, communityDay.Moves.Single().MoveId));
        Assert.All(_sample.Events, gameEvent => Assert.Null(gameEvent.TimeZone));
    }

    [Fact]
    public void Build_LaterOn_MovesEveryDateByWholeDays()
    {
        var later = DevSeed.Build(_sampleMoment.AddDays(100).AddHours(3));

        Assert.Equal(_sample.RaidBosses.Select(boss => boss.StartsAt.AddDays(100)), later.RaidBosses.Select(boss => boss.StartsAt));
        Assert.Equal(_sample.Events.Select(gameEvent => gameEvent.EndsAt?.AddDays(100)), later.Events.Select(gameEvent => gameEvent.EndsAt));
        Assert.Equal(_sample.CollectionImports.Single().UploadedAt.AddDays(100), later.CollectionImports.Single().UploadedAt);
        Assert.Equal(_sampleMoment.AddDays(100).Offset, later.JobRuns[0].StartedAt.Offset);
    }

    [Fact]
    public void FetchedEntries_HaveUniqueKeysAndASource()
    {
        var entries = _sample.RaidBosses.Cast<FetchedEntry>().Concat(_sample.MaxBosses).Concat(_sample.Events)
            .Concat(_sample.EggPoolEntries).Concat(_sample.ResearchTasks).ToList();

        Assert.Equal(entries.Count, entries.Select(entry => (entry.GetType(), entry.Key)).Distinct().Count());
        Assert.All(entries, entry =>
        {
            Assert.NotNull(entry.SourceUrl);
            Assert.Equal(entry.FirstFetchedAt, entry.FetchedAt);
            Assert.Null(entry.FixedAt);
        });
    }

    [Fact]
    public void Trainer_IsAshWithACollectionAndAReport()
    {
        var ash = Assert.Single(_sample.Users);
        var import = Assert.Single(_sample.CollectionImports);

        Assert.Equal(("Ash_PT", DevSeed.SampleGoogleSubject, "en"), (ash.DisplayName, ash.GoogleSubject, ash.Language));
        Assert.Equal(ImportStatus.Done, import.Status);
        Assert.Equal(import.RowCount, import.ImportedCount + import.UnrecognisedCount);
        Assert.Equal(_sample.CollectionPokemon.Count, import.ImportedCount);
        Assert.Equal((1, 1, 1), (import.NewCount, import.GoneCount, import.ChangedCount));
        Assert.All(_sample.CollectionPokemon, pokemon =>
        {
            Assert.Same(ash, pokemon.User);
            Assert.InRange(pokemon.AttackIv, 0, 15);
            Assert.InRange(pokemon.Level, 1m, 51m);
            Assert.True(pokemon.Cp >= 10);
        });
        Assert.Contains(_sample.CollectionPokemon, pokemon => pokemon.FormId == "zekrom" && pokemon.Alignment == Alignment.Shadow);
        Assert.Equal(4, _sample.CollectionPokemon.Count(pokemon => pokemon.FormId == "gible"));
    }

    [Fact]
    public void Collection_CpFollowsTheGameFormula()
    {
        // Gible (124/84/151) 0/15/14 at level 20: 124 × √(84 + 15) × √(151 + 14) × 0.5974² / 10 = 565.6.
        var gible = _sample.CollectionPokemon.Single(pokemon => pokemon is { FormId: "gible", AttackIv: 0 });
        var hundo = _sample.CollectionPokemon.Single(pokemon => pokemon is { FormId: "gible", AttackIv: 15 });

        Assert.Equal(565, gible.Cp);
        Assert.Equal(634, hundo.Cp); // 15/15/14, one point under the 100% egg CP of 635 in the design
    }

    [Fact]
    public void All_ListsEveryTopLevelRow()
    {
        var all = _sample.All().ToList();

        Assert.Contains(_sample.Users[0], all);
        Assert.Contains(_sample.KeeperFilters[0], all);
        Assert.Equal(all.Count, all.Distinct().Count());
    }
}
