using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyPoGoCompanion.Data.LiveData;

namespace MyPoGoCompanion.Data.Model;

// Live data is published by the weekly AI researcher. It points at game data with Restrict (the import never
// deletes a form that live data uses), and an event's children are deleted with it.

internal static class FetchedEntryModel
{
    /// <summary>The columns every fetched entry shares: unique researcher key, JSON snapshot, fix and removal.</summary>
    public static void ConfigureFetched<TEntry>(this EntityTypeBuilder<TEntry> builder)
        where TEntry : FetchedEntry
    {
        builder.HasIndex(entry => entry.Key).IsUnique();
        builder.Property(entry => entry.SourceSnapshot).HasColumnType("jsonb");
        builder.HasOne(entry => entry.FixedBy).WithMany().HasForeignKey(entry => entry.FixedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>Wall-clock start and end (see <see cref="IScheduled"/>), indexed for "now" and "coming up".</summary>
    public static void ConfigureSchedule<TEntry>(this EntityTypeBuilder<TEntry> builder, string table)
        where TEntry : class, IScheduled
    {
        builder.Property(entry => entry.StartsAt).HasColumnType("timestamp without time zone");
        builder.Property(entry => entry.EndsAt).HasColumnType("timestamp without time zone");
        builder.HasIndex(entry => new { entry.StartsAt, entry.EndsAt });
        builder.ToTable(table, t => t.HasCheckConstraint($"ck_{table}_ends_after_start", "ends_at is null or ends_at > starts_at"));
    }
}

internal sealed class RaidBossConfiguration : IEntityTypeConfiguration<RaidBoss>
{
    public void Configure(EntityTypeBuilder<RaidBoss> builder)
    {
        builder.ConfigureSchedule("raid_bosses");
        builder.ConfigureFetched();
        builder.HasOne(boss => boss.Form).WithMany().HasForeignKey(boss => boss.FormId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(boss => boss.Event).WithMany().HasForeignKey(boss => boss.EventId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class MaxBossConfiguration : IEntityTypeConfiguration<MaxBoss>
{
    public void Configure(EntityTypeBuilder<MaxBoss> builder)
    {
        builder.ConfigureSchedule("max_bosses");
        builder.ToTable("max_bosses", table => table.HasCheckConstraint("ck_max_bosses_stars", "stars between 1 and 6"));
        builder.ConfigureFetched();
        builder.HasOne(boss => boss.Form).WithMany().HasForeignKey(boss => boss.FormId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(boss => boss.Event).WithMany().HasForeignKey(boss => boss.EventId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class GameEventConfiguration : IEntityTypeConfiguration<GameEvent>
{
    public void Configure(EntityTypeBuilder<GameEvent> builder)
    {
        builder.ConfigureSchedule("events");
        builder.ConfigureFetched();
    }
}

internal sealed class EventPokemonConfiguration : IEntityTypeConfiguration<EventPokemon>
{
    public void Configure(EntityTypeBuilder<EventPokemon> builder)
    {
        builder.ToTable("event_pokemon");
        builder.HasKey(featured => new { featured.EventId, featured.FormId });
        builder.HasOne(featured => featured.Event).WithMany(gameEvent => gameEvent.Pokemon).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(featured => featured.Form).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EventMoveConfiguration : IEntityTypeConfiguration<EventMove>
{
    public void Configure(EntityTypeBuilder<EventMove> builder)
    {
        builder.ToTable("event_moves");
        builder.HasKey(eventMove => new { eventMove.EventId, eventMove.FormId, eventMove.MoveId });
        builder.HasIndex(eventMove => eventMove.MoveId);
        builder.HasOne(eventMove => eventMove.Event).WithMany(gameEvent => gameEvent.Moves).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(eventMove => eventMove.Form).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(eventMove => eventMove.Move).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EventBonusConfiguration : IEntityTypeConfiguration<EventBonus>
{
    public void Configure(EntityTypeBuilder<EventBonus> builder)
    {
        builder.ToTable("event_bonuses");
        builder.HasKey(bonus => new { bonus.EventId, bonus.SortOrder });
        builder.HasOne(bonus => bonus.Event).WithMany(gameEvent => gameEvent.Bonuses).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EggPoolEntryConfiguration : IEntityTypeConfiguration<EggPoolEntry>
{
    public void Configure(EntityTypeBuilder<EggPoolEntry> builder)
    {
        builder.ToTable("egg_pool_entries", table => table.HasCheckConstraint("ck_egg_pool_entries_rarity", "rarity between 1 and 5"));
        builder.ConfigureFetched();
        builder.HasIndex(entry => entry.Egg);
        builder.HasOne(entry => entry.Form).WithMany().HasForeignKey(entry => entry.FormId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entry => entry.Event).WithMany().HasForeignKey(entry => entry.EventId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ResearchTaskConfiguration : IEntityTypeConfiguration<ResearchTask>
{
    public void Configure(EntityTypeBuilder<ResearchTask> builder)
    {
        builder.ToTable("research_tasks");
        builder.ConfigureFetched();
        builder.HasOne(task => task.Event).WithMany().HasForeignKey(task => task.EventId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ResearchRewardConfiguration : IEntityTypeConfiguration<ResearchReward>
{
    public void Configure(EntityTypeBuilder<ResearchReward> builder)
    {
        builder.ToTable("research_rewards");
        builder.HasKey(reward => new { reward.TaskId, reward.FormId });
        builder.HasOne(reward => reward.Task).WithMany(task => task.Rewards).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(reward => reward.Form).WithMany().OnDelete(DeleteBehavior.Restrict);
    }
}
