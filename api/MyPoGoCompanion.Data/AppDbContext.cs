using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data;

/// <summary>
/// The app's PostgreSQL database. The schema is built only by the migrations in <c>Migrations/</c>
/// (applied by the deploy pipelines, and once per run by the integration tests). Tables and columns are
/// snake_case; enums are stored as their names. The tables are described in <c>docs/DATABASE.md</c>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Game data: rebuilt by the daily import.
    public DbSet<PokemonType> Types => Set<PokemonType>();

    public DbSet<Family> Families => Set<Family>();

    public DbSet<Species> Species => Set<Species>();

    public DbSet<Form> Forms => Set<Form>();

    public DbSet<Move> Moves => Set<Move>();

    public DbSet<FormMove> FormMoves => Set<FormMove>();

    public DbSet<Evolution> Evolutions => Set<Evolution>();

    public DbSet<CpMultiplier> CpMultipliers => Set<CpMultiplier>();

    // Rankings and calculated data: rebuilt by the daily import.
    public DbSet<PvpRanking> PvpRankings => Set<PvpRanking>();

    public DbSet<PvpBestIvs> PvpBestIvs => Set<PvpBestIvs>();

    public DbSet<PvpKeeperBox> PvpKeeperBoxes => Set<PvpKeeperBox>();

    public DbSet<RaidRanking> RaidRankings => Set<RaidRanking>();

    public DbSet<RaidMoveset> RaidMovesets => Set<RaidMoveset>();

    public DbSet<KeeperFilter> KeeperFilters => Set<KeeperFilter>();

    // Live data: published by the weekly AI researcher, fixable by the admin.
    public DbSet<RaidBoss> RaidBosses => Set<RaidBoss>();

    public DbSet<MaxBoss> MaxBosses => Set<MaxBoss>();

    public DbSet<GameEvent> Events => Set<GameEvent>();

    public DbSet<EventPokemon> EventPokemon => Set<EventPokemon>();

    public DbSet<EventMove> EventMoves => Set<EventMove>();

    public DbSet<EventBonus> EventBonuses => Set<EventBonus>();

    public DbSet<EggPoolEntry> EggPoolEntries => Set<EggPoolEntry>();

    public DbSet<ResearchTask> ResearchTasks => Set<ResearchTask>();

    public DbSet<ResearchReward> ResearchRewards => Set<ResearchReward>();

    // Users and what belongs to them.
    public DbSet<User> Users => Set<User>();

    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();

    public DbSet<CollectionImport> CollectionImports => Set<CollectionImport>();

    public DbSet<UnrecognisedRow> UnrecognisedRows => Set<UnrecognisedRow>();

    public DbSet<CollectionPokemon> CollectionPokemon => Set<CollectionPokemon>();

    public DbSet<CollectionChange> CollectionChanges => Set<CollectionChange>();

    // Running the system.
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    public DbSet<AiUsage> AiUsage => Set<AiUsage>();

    public DbSet<AdminFix> AdminFixes => Set<AdminFix>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IHistoryRepository, MigrationHistory>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
