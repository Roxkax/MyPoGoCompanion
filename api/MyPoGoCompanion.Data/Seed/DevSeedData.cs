using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.Seed;

/// <summary>Everything <see cref="DevSeed"/> adds, table by table (children such as event bonuses hang off their parents).</summary>
public sealed class DevSeedData
{
    public List<PokemonType> Types { get; set; } = [];

    public List<Family> Families { get; set; } = [];

    public List<Species> Species { get; set; } = [];

    public List<Form> Forms { get; set; } = [];

    public List<Move> Moves { get; set; } = [];

    public List<FormMove> FormMoves { get; set; } = [];

    public List<Evolution> Evolutions { get; set; } = [];

    public List<CpMultiplier> CpMultipliers { get; set; } = [];

    public List<PvpRanking> PvpRankings { get; set; } = [];

    public List<PvpBestIvs> PvpBestIvs { get; set; } = [];

    public List<PvpKeeperBox> PvpKeeperBoxes { get; set; } = [];

    public List<RaidRanking> RaidRankings { get; set; } = [];

    public List<KeeperFilter> KeeperFilters { get; set; } = [];

    public List<GameEvent> Events { get; set; } = [];

    public List<RaidBoss> RaidBosses { get; set; } = [];

    public List<MaxBoss> MaxBosses { get; set; } = [];

    public List<EggPoolEntry> EggPoolEntries { get; set; } = [];

    public List<ResearchTask> ResearchTasks { get; set; } = [];

    public List<User> Users { get; set; } = [];

    public List<CollectionImport> CollectionImports { get; set; } = [];

    public List<CollectionPokemon> CollectionPokemon { get; set; } = [];

    public List<SavedFilter> SavedFilters { get; set; } = [];

    public List<JobRun> JobRuns { get; set; } = [];

    public List<AiUsage> AiUsage { get; set; } = [];

    /// <summary>Every top-level entity, for <c>DbContext.AddRange</c>.</summary>
    public IEnumerable<object> All() =>
        new IEnumerable<object>[]
        {
            Types, Families, Species, Forms, Moves, FormMoves, Evolutions, CpMultipliers,
            PvpRankings, PvpBestIvs, PvpKeeperBoxes, RaidRankings, KeeperFilters,
            Events, RaidBosses, MaxBosses, EggPoolEntries, ResearchTasks,
            Users, CollectionImports, CollectionPokemon, SavedFilters, JobRuns, AiUsage,
        }.SelectMany(table => table);
}
