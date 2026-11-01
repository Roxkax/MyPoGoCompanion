using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.Model;

// Everything a user owns is deleted with the user (account deletion). Their collection points at game data
// with Restrict, so the import never deletes a form or move someone owns.

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasCheckConstraint("ck_users_language", Checks.Language));
        builder.HasIndex(user => user.GoogleSubject).IsUnique();
    }
}

internal sealed class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> builder)
    {
        builder.ToTable("saved_filters", table =>
            table.HasCheckConstraint("ck_saved_filters_name", "char_length(name) between 1 and 100"));
        builder.Property(filter => filter.Conditions).HasColumnType("jsonb");
        builder.HasIndex(filter => filter.UserId);
        builder.HasOne(filter => filter.User).WithMany().OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CollectionImportConfiguration : IEntityTypeConfiguration<CollectionImport>
{
    public void Configure(EntityTypeBuilder<CollectionImport> builder)
    {
        builder.ToTable("collection_imports", table => table.HasCheckConstraint("ck_collection_imports_counts",
            "row_count >= 0 and imported_count >= 0 and unrecognised_count >= 0"
            + " and new_count >= 0 and gone_count >= 0 and changed_count >= 0"));
        builder.HasIndex(import => new { import.UserId, import.UploadedAt });
        builder.HasOne(import => import.User).WithMany().OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UnrecognisedRowConfiguration : IEntityTypeConfiguration<UnrecognisedRow>
{
    public void Configure(EntityTypeBuilder<UnrecognisedRow> builder)
    {
        builder.ToTable("unrecognised_rows");
        builder.HasKey(row => new { row.ImportId, row.RowNumber });
        builder.HasOne(row => row.Import).WithMany(import => import.UnrecognisedRows).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CollectionPokemonConfiguration : IEntityTypeConfiguration<CollectionPokemon>
{
    public void Configure(EntityTypeBuilder<CollectionPokemon> builder)
    {
        builder.ToTable("collection_pokemon", table =>
        {
            table.HasCheckConstraint("ck_collection_pokemon_ivs", Checks.Ivs);
            table.HasCheckConstraint("ck_collection_pokemon_level", Checks.Level);
            table.HasCheckConstraint("ck_collection_pokemon_cp", "cp >= 10");
            table.HasCheckConstraint("ck_collection_pokemon_gender", "gender in ('male', 'female')");
        });
        builder.Property(pokemon => pokemon.Level).HasPrecision(3, 1);
        builder.Property(pokemon => pokemon.ChargedMove1Id).HasColumnName("charged_move1_id");
        builder.Property(pokemon => pokemon.ChargedMove2Id).HasColumnName("charged_move2_id");
        builder.HasIndex(pokemon => new { pokemon.UserId, pokemon.FormId });
        builder.HasOne(pokemon => pokemon.User).WithMany().OnDelete(DeleteBehavior.Cascade);

        // Deleting an upload never deletes the collection; deleting the user deletes both (checked at the end of
        // the statement, hence NoAction rather than Restrict).
        builder.HasOne(pokemon => pokemon.Import).WithMany().OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(pokemon => pokemon.Form).WithMany().HasForeignKey(pokemon => pokemon.FormId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(pokemon => pokemon.FastMove).WithMany().HasForeignKey(pokemon => pokemon.FastMoveId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(pokemon => pokemon.ChargedMove1).WithMany().HasForeignKey(pokemon => pokemon.ChargedMove1Id)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_collection_pokemon_moves_charged_move1_id");
        builder.HasOne(pokemon => pokemon.ChargedMove2).WithMany().HasForeignKey(pokemon => pokemon.ChargedMove2Id)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_collection_pokemon_moves_charged_move2_id");
        builder.HasIndex(pokemon => pokemon.ChargedMove1Id).HasDatabaseName("ix_collection_pokemon_charged_move1_id");
        builder.HasIndex(pokemon => pokemon.ChargedMove2Id).HasDatabaseName("ix_collection_pokemon_charged_move2_id");
    }
}

internal sealed class CollectionChangeConfiguration : IEntityTypeConfiguration<CollectionChange>
{
    public void Configure(EntityTypeBuilder<CollectionChange> builder)
    {
        builder.ToTable("collection_changes", table => table.HasCheckConstraint("ck_collection_changes_before_after",
            "(kind = 'New' and before is null and after is not null)"
            + " or (kind = 'Gone' and before is not null and after is null)"
            + " or (kind = 'Changed' and before is not null and after is not null)"));
        builder.Property(change => change.Before).HasColumnType("jsonb");
        builder.Property(change => change.After).HasColumnType("jsonb");
        builder.HasIndex(change => new { change.ImportId, change.Kind });
        builder.HasOne(change => change.Import).WithMany(import => import.Changes).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(change => change.CollectionPokemon).WithMany().OnDelete(DeleteBehavior.SetNull);

        // Changes keep the form id as history: no foreign key, so game data can move on.
    }
}
