using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.Seed;

/// <summary>
/// The local development database's data: the design's sample dataset (<c>design/source/CONVENTIONS.md</c>).
/// Game data and rankings are a real slice cut from the golden-file inputs (<c>dev-game-data.json</c>, written by
/// <c>make_dev_game_data.py</c>); live data, the sample trainer Ash_PT and their collection are written here. All
/// dates move with the clock so that "now" in the database is always the sample's Tuesday 27 October 2026, 18:30.
/// Only used in development: QA and prod get their data from the daily import and real users.
/// </summary>
public static class DevSeed
{
    /// <summary>The sample dataset's "now" (Lisbon, which is UTC at the end of October).</summary>
    public static readonly DateTime SampleNow = new(2026, 10, 27, 18, 30, 0, DateTimeKind.Unspecified);

    public const string SampleGoogleSubject = "dev-seed-ash-pt";

    private static readonly string[] _tenKmEggs = ["gible", "deino", "larvitar", "beldum", "goomy", "jangmo_o", "dreepy", "frigibax"];

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Adds the sample data to an empty database. Returns false (and changes nothing) when it has data.</summary>
    public static async Task<bool> SeedAsync(AppDbContext db, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (await db.Types.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var data = Build(now);
        db.AddRange(data.All());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Builds the sample data for the given moment, without touching a database.</summary>
    public static DevSeedData Build(DateTimeOffset now)
    {
        var data = LoadGameData();
        var clock = new SampleClock(now);
        AddLiveData(data, clock);
        AddTrainer(data, clock);
        return data;
    }

    private static DevSeedData LoadGameData()
    {
        using var stream = typeof(DevSeed).Assembly.GetManifestResourceStream("MyPoGoCompanion.Data.Seed.dev-game-data.json")
            ?? throw new InvalidOperationException("The dev seed's game data is not embedded in the assembly.");
        var file = JsonSerializer.Deserialize<GameDataFile>(stream, _jsonOptions)
            ?? throw new InvalidOperationException("The dev seed's game data is empty.");
        return new DevSeedData
        {
            Types = file.Types,
            Families = [.. file.Families.Select(id => new Family { Id = id })],
            Species = file.Species,
            Forms = file.Forms,
            Moves = file.Moves,
            FormMoves = file.FormMoves,
            Evolutions = file.Evolutions,
            CpMultipliers = file.CpMultipliers,
            PvpRankings = file.PvpRankings,
            PvpBestIvs = file.PvpBestIvs,
            PvpKeeperBoxes = file.PvpKeeperBoxes,
            RaidRankings = file.RaidRankings,
            KeeperFilters = file.KeeperFilters,
        };
    }

    private static void AddLiveData(DevSeedData data, SampleClock clock)
    {
        var fetched = clock.At(2026, 10, 25, 22, 0);
        const string LeekDuck = "https://leekduck.com/events/";

        T Fetched<T>(T entry)
            where T : FetchedEntry
        {
            entry.SourceUrl = LeekDuck;
            entry.FirstFetchedAt = fetched;
            entry.FetchedAt = fetched;
            return entry;
        }

        var halloween = Fetched(new GameEvent
        {
            Key = "event/halloween-2026",
            Kind = EventKind.Special,
            NameEn = "Halloween event",
            NamePt = "Evento de Halloween",
            SummaryEn = "Ghost-types spawn more often",
            SummaryPt = "Pokémon do tipo Fantasma aparecem mais vezes",
            StartsAt = clock.Wall(2026, 10, 20, 10, 0),
            EndsAt = clock.Wall(2026, 10, 31, 20, 0),
        });
        halloween.Bonuses.Add(new EventBonus { SortOrder = 1, TextEn = "2× catch candy", TextPt = "2× doces por captura" });
        halloween.Pokemon.Add(new EventPokemon { FormId = "litwick", Shiny = true, SortOrder = 1 });
        halloween.Pokemon.Add(new EventPokemon { FormId = "gastly", Shiny = true, SortOrder = 2 });

        var chandelureDebut = Fetched(new GameEvent
        {
            Key = "event/mega-chandelure-debut",
            Kind = EventKind.Special,
            NameEn = "Mega Chandelure debut",
            NamePt = "Estreia de Mega Chandelure",
            SummaryEn = "Mega Chandelure in Mega raids",
            SummaryPt = "Mega Chandelure em reides Mega",
            StartsAt = clock.Wall(2026, 10, 31, 10, 0),
        });
        chandelureDebut.Pokemon.Add(new EventPokemon { FormId = "chandelure_mega", Shiny = true, SortOrder = 1 });

        var communityDay = Fetched(new GameEvent
        {
            Key = "event/community-day-gible-2026-11",
            Kind = EventKind.CommunityDay,
            NameEn = "Community Day: Gible",
            NamePt = "Dia Comunitário: Gible",
            SummaryEn = "Evolve Gabite for Garchomp with Earth Power",
            SummaryPt = "Evolui Gabite para ter Garchomp com Poder da Terra",
            StartsAt = clock.Wall(2026, 11, 7, 14, 0),
            EndsAt = clock.Wall(2026, 11, 7, 17, 0),
        });
        communityDay.Pokemon.Add(new EventPokemon { FormId = "gible", Shiny = true, SortOrder = 1 });
        communityDay.Moves.Add(new EventMove
        {
            FormId = "garchomp",
            MoveId = "EARTH_POWER",
            HowEn = "Evolve Gabite 14:00–22:00",
            HowPt = "Evolui Gabite entre as 14:00 e as 22:00",
        });
        communityDay.Bonuses.Add(new EventBonus { SortOrder = 1, TextEn = "3× catch Stardust", TextPt = "3× Poeira Estelar por captura" });
        communityDay.Bonuses.Add(new EventBonus { SortOrder = 2, TextEn = "¼ egg hatch distance", TextPt = "¼ da distância para chocar ovos" });
        communityDay.Bonuses.Add(new EventBonus
        {
            SortOrder = 3,
            TextEn = "Lure Modules and Incense last 3 hours",
            TextPt = "Módulos Atrair e Incenso duram 3 horas",
        });

        GameEvent Hour(string key, EventKind kind, string nameEn, string namePt, DateTime start, string formId)
        {
            var hour = Fetched(new GameEvent
            {
                Key = key,
                Kind = kind,
                NameEn = nameEn,
                NamePt = namePt,
                StartsAt = start,
                EndsAt = start.AddHours(1),
            });
            hour.Pokemon.Add(new EventPokemon { FormId = formId, SortOrder = 1 });
            return hour;
        }

        var spotlight = Hour("event/spotlight-snorunt", EventKind.SpotlightHour, "Spotlight Hour: Snorunt", "Hora do Holofote: Snorunt",
            clock.Wall(2026, 11, 3, 18, 0), "snorunt");
        spotlight.Bonuses.Add(new EventBonus { SortOrder = 1, TextEn = "2× catch Stardust", TextPt = "2× Poeira Estelar por captura" });

        data.Events =
        [
            halloween,
            chandelureDebut,
            communityDay,
            spotlight,
            Hour("event/raid-hour-kyogre", EventKind.RaidHour, "Raid Hour: Kyogre", "Hora de Reides: Kyogre",
                clock.Wall(2026, 10, 28, 18, 0), "kyogre"),
            Hour("event/max-monday-lapras", EventKind.MaxBattle, "Max Monday: Gigantamax Lapras", "Segunda Max: Lapras Gigantamax",
                clock.Wall(2026, 11, 2, 18, 0), "lapras"),
            Hour("event/raid-hour-groudon", EventKind.RaidHour, "Raid Hour: Groudon", "Hora de Reides: Groudon",
                clock.Wall(2026, 11, 4, 18, 0), "groudon"),
        ];

        var rotationStart = clock.Wall(2026, 10, 27, 10, 0);
        var rotationEnd = clock.Wall(2026, 11, 3, 10, 0);
        RaidBoss Raid(string formId, RaidTier tier, bool shiny, bool shadow = false, DateTime? start = null, DateTime? end = null,
            GameEvent? gameEvent = null) => Fetched(new RaidBoss
            {
                Key = $"raid/{formId}{(shadow ? "-shadow" : string.Empty)}/{tier}/{(start ?? rotationStart):yyyy-MM-dd}",
                FormId = formId,
                IsShadow = shadow,
                Tier = tier,
                ShinyAvailable = shiny,
                StartsAt = start ?? rotationStart,
                EndsAt = end ?? rotationEnd,
                Event = gameEvent,
            });

        var chandelureStart = clock.Wall(2026, 10, 31, 10, 0);
        data.RaidBosses =
        [
            Raid("kyogre", RaidTier.FiveStar, shiny: true),
            Raid("gengar_mega", RaidTier.Mega, shiny: true, end: chandelureStart, gameEvent: halloween),
            Raid("absol", RaidTier.ThreeStar, shiny: true, shadow: true),
            Raid("charizard", RaidTier.ThreeStar, shiny: true),
            Raid("lapras", RaidTier.ThreeStar, shiny: true),
            Raid("gible", RaidTier.OneStar, shiny: true),
            Raid("charmander", RaidTier.OneStar, shiny: true),
            Raid("chandelure_mega", RaidTier.Mega, shiny: true, start: chandelureStart, end: clock.Wall(2026, 11, 10, 10, 0),
                gameEvent: chandelureDebut),
            Raid("groudon", RaidTier.FiveStar, shiny: true, start: rotationEnd, end: clock.Wall(2026, 11, 10, 10, 0)),
        ];

        var maxStart = clock.Wall(2026, 10, 25, 10, 0);
        var maxEnd = clock.Wall(2026, 11, 1, 10, 0);
        MaxBoss Max(string formId, int stars, bool gigantamax, DateTime start, DateTime end) => Fetched(new MaxBoss
        {
            Key = $"max/{formId}/{stars}/{start:yyyy-MM-dd}",
            FormId = formId,
            Stars = stars,
            IsGigantamax = gigantamax,
            ShinyAvailable = true,
            StartsAt = start,
            EndsAt = end,
        });
        data.MaxBosses =
        [
            Max("venusaur", 6, gigantamax: true, maxStart, maxEnd),
            Max("charmander", 1, gigantamax: false, maxStart, maxEnd),
            Max("lapras", 6, gigantamax: true, maxEnd, clock.Wall(2026, 11, 8, 10, 0)),
        ];

        data.EggPoolEntries =
        [
            .. _tenKmEggs.Select(formId =>
                Fetched(new EggPoolEntry
                {
                    Key = $"egg/TenKm/{formId}",
                    Egg = EggKind.TenKm,
                    FormId = formId,
                    ShinyAvailable = formId != "frigibax",
                })),
        ];

        ResearchTask Task(string key, string taskEn, string taskPt, string formId, GameEvent? gameEvent = null)
        {
            var task = Fetched(new ResearchTask { Key = key, TaskEn = taskEn, TaskPt = taskPt, Event = gameEvent });
            task.Rewards.Add(new ResearchReward { FormId = formId, ShinyAvailable = true });
            return task;
        }

        data.ResearchTasks =
        [
            Task("research/win-3-raids", "Win 3 raids", "Vence 3 reides", "gible"),
            Task("research/catch-10-ghost", "Catch 10 Ghost-type Pokémon", "Apanha 10 Pokémon do tipo Fantasma", "litwick", halloween),
            Task("research/3-great-throws", "Make 3 Great Throws", "Faz 3 lançamentos Ótimos", "snorunt"),
            Task("research/power-up-5", "Power up Pokémon 5 times", "Fortalece Pokémon 5 vezes", "bulbasaur"),
            Task("research/catch-15-gible", "Catch 15 Gible", "Apanha 15 Gible", "gible", communityDay),
        ];
    }

    private static void AddTrainer(DevSeedData data, SampleClock clock)
    {
        var ash = new User
        {
            GoogleSubject = SampleGoogleSubject,
            Email = "ash.pt@example.com",
            DisplayName = "Ash_PT",
            CreatedAt = clock.At(2026, 10, 1, 12, 0),
            LastSignInAt = clock.At(2026, 10, 27, 18, 0),
        };
        data.Users = [ash];

        var import = new CollectionImport
        {
            User = ash,
            UploadedAt = clock.At(2026, 10, 25, 21, 14),
            FinishedAt = clock.At(2026, 10, 25, 21, 15),
            FileName = "PokeGenie_export_2026-10-25.csv",
            Status = ImportStatus.Done,
        };
        import.UnrecognisedRows.Add(new UnrecognisedRow
        {
            RowNumber = 812,
            Name = "Pikachu",
            Form = "Flying 5th Anniversary",
            Cp = 412,
            Reason = "This costume isn't in the Pokédex yet.",
        });
        data.CollectionImports = [import];

        var multipliers = data.CpMultipliers.ToDictionary(multiplier => multiplier.Level, multiplier => multiplier.Value);
        var forms = data.Forms.ToDictionary(form => form.Id);
        var moves = data.Moves.ToDictionary(move => move.Id);
        var learnsets = data.FormMoves.ToLookup(formMove => formMove.FormId, formMove => moves[formMove.MoveId]);

        CollectionPokemon Mine(string formId, int attack, int defense, int stamina, decimal level, Alignment alignment = Alignment.Normal,
            string? fast = null, string? charged = null, bool shiny = false, bool lucky = false, bool favourite = false, int caughtDaysAgo = 30)
        {
            var form = forms[formId];
            var cpm = multipliers[level];
            var cp = (int)Math.Floor((form.Attack + attack) * Math.Sqrt(form.Defense + defense) * Math.Sqrt(form.Stamina + stamina) * cpm * cpm / 10);
            var learnset = learnsets[formId].ToList();
            return new CollectionPokemon
            {
                User = ash,
                Import = import,
                FormId = formId,
                Alignment = alignment,
                Cp = Math.Max(10, cp),
                AttackIv = attack,
                DefenseIv = defense,
                StaminaIv = stamina,
                Level = level,
                FastMoveId = fast ?? learnset.First(move => move.Kind == MoveKind.Fast).Id,
                ChargedMove1Id = charged ?? learnset.First(move => move.Kind == MoveKind.Charged).Id,
                IsShiny = shiny,
                IsLucky = lucky,
                IsFavourite = favourite,
                CaughtOn = DateOnly.FromDateTime(clock.Wall(2026, 10, 27, 0, 0).AddDays(-caughtDaysAgo)),
            };
        }

        var gible = Mine("gible", 0, 15, 14, 20m, caughtDaysAgo: 2);
        var garchomp = Mine("garchomp", 2, 15, 13, 23m, fast: "MUD_SHOT", charged: "EARTH_POWER", favourite: true);
        data.CollectionPokemon =
        [
            garchomp,
            Mine("gible", 15, 15, 14, 20m, shiny: true),
            gible,
            Mine("gible", 1, 13, 15, 15m),
            Mine("gible", 10, 4, 2, 10m),
            Mine("gabite", 12, 12, 13, 25m),
            Mine("medicham", 1, 15, 15, 50m, fast: "COUNTER", charged: "ICE_PUNCH", lucky: true),
            Mine("zekrom", 13, 15, 14, 35m, Alignment.Shadow, fast: "CHARGE_BEAM", charged: "WILD_CHARGE"),
            Mine("zeraora", 15, 15, 14, 40m, fast: "SPARK", charged: "WILD_CHARGE"),
            Mine("sceptile", 14, 15, 13, 40m, fast: "BULLET_SEED", charged: "FRENZY_PLANT"),
            Mine("magnezone", 15, 13, 14, 40m, fast: "SPARK", charged: "WILD_CHARGE"),
            Mine("torterra", 14, 14, 15, 40m, fast: "RAZOR_LEAF", charged: "FRENZY_PLANT"),
            Mine("venusaur", 13, 14, 15, 35m, fast: "VINE_WHIP", charged: "FRENZY_PLANT"),
            Mine("altaria", 0, 14, 15, 26m, fast: "DRAGON_BREATH", charged: "SKY_ATTACK"),
            Mine("azumarill", 0, 15, 14, 40m, fast: "BUBBLE", charged: "ICE_BEAM"),
        ];

        import.RowCount = data.CollectionPokemon.Count + import.UnrecognisedRows.Count;
        import.ImportedCount = data.CollectionPokemon.Count;
        import.UnrecognisedCount = import.UnrecognisedRows.Count;
        import.Changes.Add(new CollectionChange
        {
            Kind = ChangeKind.New,
            CollectionPokemon = gible,
            FormId = "gible",
            After = $$"""{"cp":{{gible.Cp}},"ivs":[0,15,14],"level":20}""",
        });
        import.Changes.Add(new CollectionChange
        {
            Kind = ChangeKind.Gone,
            FormId = "charmander",
            Before = """{"cp":512,"ivs":[3,1,0],"level":20}""",
        });
        import.Changes.Add(new CollectionChange
        {
            Kind = ChangeKind.Changed,
            CollectionPokemon = garchomp,
            FormId = "garchomp",
            What = ["powered-up", "new-moves"],
            Before = """{"cp":2263,"level":20,"charged":["OUTRAGE"]}""",
            After = $$"""{"cp":{{garchomp.Cp}},"level":23,"charged":["EARTH_POWER"]}""",
        });
        import.NewCount = 1;
        import.GoneCount = 1;
        import.ChangedCount = 1;

        data.SavedFilters =
        [
            new SavedFilter
            {
                User = ash,
                Name = "Hundos",
                SearchString = "4*",
                CreatedAt = clock.At(2026, 10, 10, 20, 0),
                UpdatedAt = clock.At(2026, 10, 10, 20, 0),
            },
            new SavedFilter
            {
                User = ash,
                Name = "Gible family for Great League",
                SearchString = "443-445&0-1attack&3-4defense&2-4hp",
                Conditions = """{"conditions":[{"group":"pokedex","values":["443-445"]},{"group":"attack","values":["0-1"]},{"group":"defense","values":["3-4"]},{"group":"hp","values":["2-4"]}]}""",
                CreatedAt = clock.At(2026, 10, 26, 9, 30),
                UpdatedAt = clock.At(2026, 10, 26, 9, 30),
            },
        ];

        var dailyImport = new JobRun
        {
            Job = JobKind.DailyImport,
            Status = JobStatus.Succeeded,
            StartedAt = clock.At(2026, 10, 27, 6, 0),
            FinishedAt = clock.At(2026, 10, 27, 6, 4),
            Summary = """{"forms":0,"moves":1,"rankings":214}""",
        };
        var researcher = new JobRun
        {
            Job = JobKind.AiResearcher,
            Status = JobStatus.Succeeded,
            StartedAt = clock.At(2026, 10, 25, 22, 0),
            FinishedAt = clock.At(2026, 10, 25, 22, 6),
            Summary = """{"raidBosses":9,"maxBosses":3,"events":7,"eggs":8,"research":5}""",
        };
        data.JobRuns = [dailyImport, researcher];
        data.AiUsage =
        [
            new AiUsage
            {
                At = clock.At(2026, 10, 25, 22, 5),
                Purpose = AiPurpose.Researcher,
                JobRun = researcher,
                Model = "sample-large-model",
                InputTokens = 182_000,
                OutputTokens = 9_400,
                CostUsd = 0.687m,
            },
            new AiUsage
            {
                At = clock.At(2026, 10, 27, 18, 10),
                Purpose = AiPurpose.Chat,
                User = ash,
                Model = "sample-small-model",
                InputTokens = 6_200,
                OutputTokens = 410,
                CostUsd = 0.0083m,
            },
        ];
    }

    /// <summary>Moves the sample's dates by whole days so that the sample's "now" falls on today.</summary>
    private sealed class SampleClock(DateTimeOffset now)
    {
        private readonly int _days = (now.UtcDateTime.Date - SampleNow.Date).Days;

        /// <summary>A wall-clock time (local time wherever the player is).</summary>
        public DateTime Wall(int year, int month, int day, int hour, int minute) =>
            new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified).AddDays(_days);

        /// <summary>A moment (Lisbon is UTC at the end of October).</summary>
        public DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
            new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero).AddDays(_days);
    }

    private sealed class GameDataFile
    {
        public List<PokemonType> Types { get; init; } = [];

        public List<string> Families { get; init; } = [];

        public List<Species> Species { get; init; } = [];

        public List<Form> Forms { get; init; } = [];

        public List<Move> Moves { get; init; } = [];

        public List<FormMove> FormMoves { get; init; } = [];

        public List<Evolution> Evolutions { get; init; } = [];

        public List<CpMultiplier> CpMultipliers { get; init; } = [];

        public List<PvpRanking> PvpRankings { get; init; } = [];

        public List<PvpBestIvs> PvpBestIvs { get; init; } = [];

        public List<PvpKeeperBox> PvpKeeperBoxes { get; init; } = [];

        public List<RaidRanking> RaidRankings { get; init; } = [];

        public List<KeeperFilter> KeeperFilters { get; init; } = [];
    }
}
