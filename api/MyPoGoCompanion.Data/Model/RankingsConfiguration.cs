using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.Rankings;

namespace MyPoGoCompanion.Data.Model;

// Rankings are calculated by the daily import from game data; deleting a form or move deletes its rankings.

internal sealed class PvpRankingConfiguration : IEntityTypeConfiguration<PvpRanking>
{
    public void Configure(EntityTypeBuilder<PvpRanking> builder)
    {
        builder.ToTable("pvp_rankings", table => table.HasCheckConstraint("ck_pvp_rankings_rank", "rank >= 1"));
        builder.HasKey(ranking => new { ranking.League, ranking.FormId, ranking.IsShadow });
        builder.HasIndex(ranking => new { ranking.League, ranking.Rank });
        builder.Property(ranking => ranking.ChargedMove1Id).HasColumnName("charged_move1_id");
        builder.Property(ranking => ranking.ChargedMove2Id).HasColumnName("charged_move2_id");
        builder.HasOne(ranking => ranking.Form).WithMany().OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Move>().WithMany().HasForeignKey(ranking => ranking.FastMoveId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Move>().WithMany().HasForeignKey(ranking => ranking.ChargedMove1Id).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pvp_rankings_moves_charged_move1_id");
        builder.HasOne<Move>().WithMany().HasForeignKey(ranking => ranking.ChargedMove2Id).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pvp_rankings_moves_charged_move2_id");
    }
}

internal sealed class PvpBestIvsConfiguration : IEntityTypeConfiguration<PvpBestIvs>
{
    public void Configure(EntityTypeBuilder<PvpBestIvs> builder)
    {
        builder.ToTable("pvp_best_ivs", table =>
        {
            table.HasCheckConstraint("ck_pvp_best_ivs_ivs", Checks.Ivs);
            table.HasCheckConstraint("ck_pvp_best_ivs_level", Checks.Level);
        });
        builder.HasKey(best => new { best.League, best.FormId });
        builder.Property(best => best.Level).HasPrecision(3, 1);
        builder.HasOne(best => best.Form).WithMany().OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PvpKeeperBoxConfiguration : IEntityTypeConfiguration<PvpKeeperBox>
{
    public void Configure(EntityTypeBuilder<PvpKeeperBox> builder)
    {
        builder.ToTable("pvp_keeper_boxes", table => table.HasCheckConstraint("ck_pvp_keeper_boxes_bars",
            "attack_bar_min between 0 and attack_bar_max and attack_bar_max <= 4"
            + " and defense_bar_min between 0 and defense_bar_max and defense_bar_max <= 4"
            + " and stamina_bar_min between 0 and stamina_bar_max and stamina_bar_max <= 4"));
        builder.HasKey(box => new { box.League, box.Dex });
        builder.HasOne<Species>().WithMany().HasForeignKey(box => box.Dex).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RaidRankingConfiguration : IEntityTypeConfiguration<RaidRanking>
{
    public void Configure(EntityTypeBuilder<RaidRanking> builder)
    {
        builder.ToTable("raid_rankings", table => table.HasCheckConstraint("ck_raid_rankings_rank", "rank >= 1"));
        builder.HasKey(ranking => new { ranking.AttackingTypeId, ranking.FormId, ranking.IsShadow });
        builder.HasIndex(ranking => new { ranking.AttackingTypeId, ranking.Rank });
        builder.HasIndex(ranking => ranking.FormId);
        builder.HasOne(ranking => ranking.AttackingType).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(ranking => ranking.Form).WithMany().OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Move>().WithMany().HasForeignKey(ranking => ranking.FastMoveId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Move>().WithMany().HasForeignKey(ranking => ranking.ChargedMoveId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RaidMovesetConfiguration : IEntityTypeConfiguration<RaidMoveset>
{
    public void Configure(EntityTypeBuilder<RaidMoveset> builder)
    {
        builder.ToTable("raid_movesets", table => table.HasCheckConstraint("ck_raid_movesets_rank", "rank >= 1"));
        builder.HasKey(moveset => new { moveset.FormId, moveset.IsShadow, moveset.FastMoveId, moveset.ChargedMoveId });
        builder.HasOne(moveset => moveset.Form).WithMany().OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Move>().WithMany().HasForeignKey(moveset => moveset.FastMoveId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Move>().WithMany().HasForeignKey(moveset => moveset.ChargedMoveId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class KeeperFilterConfiguration : IEntityTypeConfiguration<KeeperFilter>
{
    public void Configure(EntityTypeBuilder<KeeperFilter> builder)
    {
        builder.ToTable("keeper_filters", table =>
        {
            table.HasCheckConstraint("ck_keeper_filters_part", "part >= 1");
            table.HasCheckConstraint("ck_keeper_filters_length", "char_length(search_string) between 1 and 5000");
        });
        builder.HasKey(filter => new { filter.Kind, filter.Variant, filter.Part });
    }
}
