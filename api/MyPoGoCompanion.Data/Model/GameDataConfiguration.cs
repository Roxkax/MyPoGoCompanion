using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Data.Model;

// Game data is only written by the daily import. Rows inside it reference each other with cascading deletes;
// rows that users or the AI researcher point at are never deleted (the import marks them instead).

internal sealed class PokemonTypeConfiguration : IEntityTypeConfiguration<PokemonType>
{
    public void Configure(EntityTypeBuilder<PokemonType> builder)
    {
        builder.ToTable("types");
        builder.HasIndex(type => type.SortOrder).IsUnique();
    }
}

internal sealed class FamilyConfiguration : IEntityTypeConfiguration<Family>
{
    public void Configure(EntityTypeBuilder<Family> builder) => builder.ToTable("families");
}

internal sealed class SpeciesConfiguration : IEntityTypeConfiguration<Species>
{
    public void Configure(EntityTypeBuilder<Species> builder)
    {
        builder.ToTable("species", table =>
        {
            table.HasCheckConstraint("ck_species_dex", "dex > 0");
            table.HasCheckConstraint("ck_species_generation", "generation between 1 and 99");
        });
        builder.HasKey(species => species.Dex);
        builder.Property(species => species.Dex).ValueGeneratedNever();
        builder.HasOne(species => species.Family).WithMany(family => family.Species).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormConfiguration : IEntityTypeConfiguration<Form>
{
    public void Configure(EntityTypeBuilder<Form> builder)
    {
        builder.ToTable("forms", table =>
        {
            table.HasCheckConstraint("ck_forms_stats", "attack > 0 and defense > 0 and stamina > 0");
            table.HasCheckConstraint("ck_forms_two_different_types", "type2_id is null or type2_id <> type1_id");
        });
        builder.Property(form => form.Type1Id).HasColumnName("type1_id");
        builder.Property(form => form.Type2Id).HasColumnName("type2_id");
        builder.Property(form => form.MaxCp50).HasColumnName("max_cp_50");
        builder.HasOne(form => form.Species).WithMany(species => species.Forms).HasForeignKey(form => form.Dex)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(form => form.Type1).WithMany().HasForeignKey(form => form.Type1Id).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_forms_types_type1_id");
        builder.HasOne(form => form.Type2).WithMany().HasForeignKey(form => form.Type2Id).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_forms_types_type2_id");
        builder.HasIndex(form => form.Type1Id).HasDatabaseName("ix_forms_type1_id");
        builder.HasIndex(form => form.Type2Id).HasDatabaseName("ix_forms_type2_id");
        builder.HasOne(form => form.BaseForm).WithMany().HasForeignKey(form => form.BaseFormId).OnDelete(DeleteBehavior.Restrict);

        // One default form per species: the Pokédex line.
        builder.HasIndex(form => form.Dex).IsUnique().HasFilter("is_default").HasDatabaseName("ix_forms_default_per_species");
        builder.HasIndex(form => new { form.Dex, form.Id });
        builder.HasIndex(form => form.GameMasterId);
    }
}

internal sealed class MoveConfiguration : IEntityTypeConfiguration<Move>
{
    public void Configure(EntityTypeBuilder<Move> builder)
    {
        builder.ToTable("moves", table =>
        {
            table.HasCheckConstraint("ck_moves_pvp_turns", "kind <> 'Fast' or pvp_turns >= 1");
            table.HasCheckConstraint("ck_moves_buff_chance", "pvp_buff_chance is null or pvp_buff_chance between 0 and 1");
        });
        builder.HasOne(move => move.Type).WithMany().HasForeignKey(move => move.TypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FormMoveConfiguration : IEntityTypeConfiguration<FormMove>
{
    public void Configure(EntityTypeBuilder<FormMove> builder)
    {
        builder.ToTable("form_moves");
        builder.HasKey(formMove => new { formMove.FormId, formMove.MoveId });
        builder.HasOne(formMove => formMove.Form).WithMany(form => form.Moves).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(formMove => formMove.Move).WithMany().OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvolutionConfiguration : IEntityTypeConfiguration<Evolution>
{
    public void Configure(EntityTypeBuilder<Evolution> builder)
    {
        builder.ToTable("evolutions", table =>
        {
            table.HasCheckConstraint("ck_evolutions_candy", "candy >= 0 and (candy_after_trade is null or candy_after_trade >= 0)");
            table.HasCheckConstraint("ck_evolutions_time_of_day", "time_of_day in ('day', 'night')");
            table.HasCheckConstraint("ck_evolutions_gender", "gender in ('male', 'female')");
        });
        builder.HasOne(evolution => evolution.FromForm).WithMany().HasForeignKey(evolution => evolution.FromFormId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(evolution => evolution.ToForm).WithMany().HasForeignKey(evolution => evolution.ToFormId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CpMultiplierConfiguration : IEntityTypeConfiguration<CpMultiplier>
{
    public void Configure(EntityTypeBuilder<CpMultiplier> builder)
    {
        builder.ToTable("cp_multipliers", table =>
            table.HasCheckConstraint("ck_cp_multipliers_level", "level between 1 and 55 and level * 2 = floor(level * 2)"));
        builder.HasKey(multiplier => multiplier.Level);
        builder.Property(multiplier => multiplier.Level).HasPrecision(3, 1);
    }
}
