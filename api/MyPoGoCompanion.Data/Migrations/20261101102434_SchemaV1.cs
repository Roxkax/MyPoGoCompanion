using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyPoGoCompanion.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemaV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cp_multipliers",
                columns: table => new
                {
                    level = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cp_multipliers", x => x.level);
                    table.CheckConstraint("ck_cp_multipliers_level", "level between 1 and 55 and level * 2 = floor(level * 2)");
                });

            migrationBuilder.CreateTable(
                name: "families",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_families", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "keeper_filters",
                columns: table => new
                {
                    kind = table.Column<string>(type: "text", nullable: false),
                    variant = table.Column<string>(type: "text", nullable: false),
                    part = table.Column<int>(type: "integer", nullable: false),
                    from_dex = table.Column<int>(type: "integer", nullable: true),
                    to_dex = table.Column<int>(type: "integer", nullable: true),
                    search_string = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_keeper_filters", x => new { x.kind, x.variant, x.part });
                    table.CheckConstraint("ck_keeper_filters_length", "char_length(search_string) between 1 and 5000");
                    table.CheckConstraint("ck_keeper_filters_part", "part >= 1");
                });

            migrationBuilder.CreateTable(
                name: "types",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_pt = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    google_subject = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    picture_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sign_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    theme = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_language", "language in ('en', 'pt-PT')");
                });

            migrationBuilder.CreateTable(
                name: "species",
                columns: table => new
                {
                    dex = table.Column<int>(type: "integer", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_pt = table.Column<string>(type: "text", nullable: true),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    family_id = table.Column<string>(type: "text", nullable: false),
                    nicknames = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_species", x => x.dex);
                    table.CheckConstraint("ck_species_dex", "dex > 0");
                    table.CheckConstraint("ck_species_generation", "generation between 1 and 99");
                    table.ForeignKey(
                        name: "fk_species_families_family_id",
                        column: x => x.family_id,
                        principalTable: "families",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moves",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    type_id = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_pt = table.Column<string>(type: "text", nullable: true),
                    raid_power = table.Column<double>(type: "double precision", nullable: false),
                    raid_duration_ms = table.Column<int>(type: "integer", nullable: false),
                    raid_damage_window_start_ms = table.Column<int>(type: "integer", nullable: false),
                    raid_damage_window_end_ms = table.Column<int>(type: "integer", nullable: false),
                    raid_energy_delta = table.Column<int>(type: "integer", nullable: false),
                    pvp_power = table.Column<double>(type: "double precision", nullable: false),
                    pvp_energy_delta = table.Column<int>(type: "integer", nullable: false),
                    pvp_turns = table.Column<int>(type: "integer", nullable: true),
                    pvp_buff_chance = table.Column<double>(type: "double precision", nullable: true),
                    pvp_self_attack_stages = table.Column<int>(type: "integer", nullable: false),
                    pvp_self_defense_stages = table.Column<int>(type: "integer", nullable: false),
                    pvp_opponent_attack_stages = table.Column<int>(type: "integer", nullable: false),
                    pvp_opponent_defense_stages = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moves", x => x.id);
                    table.CheckConstraint("ck_moves_buff_chance", "pvp_buff_chance is null or pvp_buff_chance between 0 and 1");
                    table.CheckConstraint("ck_moves_pvp_turns", "kind <> 'Fast' or pvp_turns >= 1");
                    table.ForeignKey(
                        name: "fk_moves_types_type_id",
                        column: x => x.type_id,
                        principalTable: "types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "admin_fixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    entry_table = table.Column<string>(type: "text", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_fixes", x => x.id);
                    table.ForeignKey(
                        name: "fk_admin_fixes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "collection_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    imported_count = table.Column<int>(type: "integer", nullable: false),
                    unrecognised_count = table.Column<int>(type: "integer", nullable: false),
                    new_count = table.Column<int>(type: "integer", nullable: false),
                    gone_count = table.Column<int>(type: "integer", nullable: false),
                    changed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_imports", x => x.id);
                    table.CheckConstraint("ck_collection_imports_counts", "row_count >= 0 and imported_count >= 0 and unrecognised_count >= 0 and new_count >= 0 and gone_count >= 0 and changed_count >= 0");
                    table.ForeignKey(
                        name: "fk_collection_imports_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_pt = table.Column<string>(type: "text", nullable: true),
                    summary_en = table.Column<string>(type: "text", nullable: true),
                    summary_pt = table.Column<string>(type: "text", nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    time_zone = table.Column<string>(type: "text", nullable: true),
                    key = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    source_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    first_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_events", x => x.id);
                    table.CheckConstraint("ck_events_ends_after_start", "ends_at is null or ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_events_users_fixed_by_user_id",
                        column: x => x.fixed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "job_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    summary = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_runs", x => x.id);
                    table.CheckConstraint("ck_job_runs_finished", "(status = 'Running') = (finished_at is null)");
                    table.ForeignKey(
                        name: "fk_job_runs_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "saved_filters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    search_string = table.Column<string>(type: "text", nullable: false),
                    conditions = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_filters", x => x.id);
                    table.CheckConstraint("ck_saved_filters_name", "char_length(name) between 1 and 100");
                    table.ForeignKey(
                        name: "fk_saved_filters_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "forms",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    game_master_id = table.Column<string>(type: "text", nullable: true),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_pt = table.Column<string>(type: "text", nullable: true),
                    type1_id = table.Column<string>(type: "text", nullable: false),
                    type2_id = table.Column<string>(type: "text", nullable: true),
                    attack = table.Column<int>(type: "integer", nullable: false),
                    defense = table.Column<int>(type: "integer", nullable: false),
                    stamina = table.Column<int>(type: "integer", nullable: false),
                    max_cp_50 = table.Column<int>(type: "integer", nullable: false),
                    base_form_id = table.Column<string>(type: "text", nullable: true),
                    released = table.Column<bool>(type: "boolean", nullable: false),
                    shadow_released = table.Column<bool>(type: "boolean", nullable: false),
                    shiny_released = table.Column<bool>(type: "boolean", nullable: true),
                    can_dynamax = table.Column<bool>(type: "boolean", nullable: false),
                    can_gigantamax = table.Column<bool>(type: "boolean", nullable: false),
                    buddy_distance_km = table.Column<int>(type: "integer", nullable: false),
                    third_move_stardust = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forms", x => x.id);
                    table.CheckConstraint("ck_forms_stats", "attack > 0 and defense > 0 and stamina > 0");
                    table.CheckConstraint("ck_forms_two_different_types", "type2_id is null or type2_id <> type1_id");
                    table.ForeignKey(
                        name: "fk_forms_forms_base_form_id",
                        column: x => x.base_form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forms_species_dex",
                        column: x => x.dex,
                        principalTable: "species",
                        principalColumn: "dex",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forms_types_type1_id",
                        column: x => x.type1_id,
                        principalTable: "types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_forms_types_type2_id",
                        column: x => x.type2_id,
                        principalTable: "types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pvp_keeper_boxes",
                columns: table => new
                {
                    league = table.Column<string>(type: "text", nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    attack_bar_min = table.Column<int>(type: "integer", nullable: false),
                    attack_bar_max = table.Column<int>(type: "integer", nullable: false),
                    defense_bar_min = table.Column<int>(type: "integer", nullable: false),
                    defense_bar_max = table.Column<int>(type: "integer", nullable: false),
                    stamina_bar_min = table.Column<int>(type: "integer", nullable: false),
                    stamina_bar_max = table.Column<int>(type: "integer", nullable: false),
                    judged_by = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pvp_keeper_boxes", x => new { x.league, x.dex });
                    table.CheckConstraint("ck_pvp_keeper_boxes_bars", "attack_bar_min between 0 and attack_bar_max and attack_bar_max <= 4 and defense_bar_min between 0 and defense_bar_max and defense_bar_max <= 4 and stamina_bar_min between 0 and stamina_bar_max and stamina_bar_max <= 4");
                    table.ForeignKey(
                        name: "fk_pvp_keeper_boxes_species_dex",
                        column: x => x.dex,
                        principalTable: "species",
                        principalColumn: "dex",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unrecognised_rows",
                columns: table => new
                {
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    form = table.Column<string>(type: "text", nullable: true),
                    cp = table.Column<int>(type: "integer", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unrecognised_rows", x => new { x.import_id, x.row_number });
                    table.ForeignKey(
                        name: "fk_unrecognised_rows_collection_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "collection_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "event_bonuses",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    text_en = table.Column<string>(type: "text", nullable: false),
                    text_pt = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_bonuses", x => new { x.event_id, x.sort_order });
                    table.ForeignKey(
                        name: "fk_event_bonuses_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "research_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_en = table.Column<string>(type: "text", nullable: false),
                    task_pt = table.Column<string>(type: "text", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    source_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    first_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_research_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_research_tasks_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_research_tasks_users_fixed_by_user_id",
                        column: x => x.fixed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    job_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    model = table.Column<string>(type: "text", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usage", x => x.id);
                    table.CheckConstraint("ck_ai_usage_amounts", "input_tokens >= 0 and output_tokens >= 0 and cost_usd >= 0");
                    table.ForeignKey(
                        name: "fk_ai_usage_job_runs_job_run_id",
                        column: x => x.job_run_id,
                        principalTable: "job_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ai_usage_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "collection_pokemon",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    alignment = table.Column<string>(type: "text", nullable: false),
                    nickname = table.Column<string>(type: "text", nullable: true),
                    cp = table.Column<int>(type: "integer", nullable: false),
                    attack_iv = table.Column<int>(type: "integer", nullable: false),
                    defense_iv = table.Column<int>(type: "integer", nullable: false),
                    stamina_iv = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    fast_move_id = table.Column<string>(type: "text", nullable: true),
                    charged_move1_id = table.Column<string>(type: "text", nullable: true),
                    charged_move2_id = table.Column<string>(type: "text", nullable: true),
                    is_shiny = table.Column<bool>(type: "boolean", nullable: false),
                    is_lucky = table.Column<bool>(type: "boolean", nullable: false),
                    is_favourite = table.Column<bool>(type: "boolean", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: true),
                    caught_on = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_pokemon", x => x.id);
                    table.CheckConstraint("ck_collection_pokemon_cp", "cp >= 10");
                    table.CheckConstraint("ck_collection_pokemon_gender", "gender in ('male', 'female')");
                    table.CheckConstraint("ck_collection_pokemon_ivs", "attack_iv between 0 and 15 and defense_iv between 0 and 15 and stamina_iv between 0 and 15");
                    table.CheckConstraint("ck_collection_pokemon_level", "level between 1 and 51 and level * 2 = floor(level * 2)");
                    table.ForeignKey(
                        name: "fk_collection_pokemon_collection_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "collection_imports",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_collection_pokemon_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_collection_pokemon_moves_charged_move1_id",
                        column: x => x.charged_move1_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_collection_pokemon_moves_charged_move2_id",
                        column: x => x.charged_move2_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_collection_pokemon_moves_fast_move_id",
                        column: x => x.fast_move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_collection_pokemon_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "egg_pool_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    egg = table.Column<string>(type: "text", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    shiny_available = table.Column<bool>(type: "boolean", nullable: false),
                    rarity = table.Column<int>(type: "integer", nullable: true),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    source_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    first_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_egg_pool_entries", x => x.id);
                    table.CheckConstraint("ck_egg_pool_entries_rarity", "rarity between 1 and 5");
                    table.ForeignKey(
                        name: "fk_egg_pool_entries_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_egg_pool_entries_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_egg_pool_entries_users_fixed_by_user_id",
                        column: x => x.fixed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "event_moves",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    move_id = table.Column<string>(type: "text", nullable: false),
                    how_en = table.Column<string>(type: "text", nullable: true),
                    how_pt = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_moves", x => new { x.event_id, x.form_id, x.move_id });
                    table.ForeignKey(
                        name: "fk_event_moves_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_moves_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_event_moves_moves_move_id",
                        column: x => x.move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event_pokemon",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    shiny = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_pokemon", x => new { x.event_id, x.form_id });
                    table.ForeignKey(
                        name: "fk_event_pokemon_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_event_pokemon_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evolutions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_form_id = table.Column<string>(type: "text", nullable: false),
                    to_form_id = table.Column<string>(type: "text", nullable: false),
                    candy = table.Column<int>(type: "integer", nullable: false),
                    candy_after_trade = table.Column<int>(type: "integer", nullable: true),
                    item_id = table.Column<string>(type: "text", nullable: true),
                    buddy_distance_km = table.Column<int>(type: "integer", nullable: true),
                    time_of_day = table.Column<string>(type: "text", nullable: true),
                    lure_id = table.Column<string>(type: "text", nullable: true),
                    gender = table.Column<string>(type: "text", nullable: true),
                    task_en = table.Column<string>(type: "text", nullable: true),
                    task_pt = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evolutions", x => x.id);
                    table.CheckConstraint("ck_evolutions_candy", "candy >= 0 and (candy_after_trade is null or candy_after_trade >= 0)");
                    table.CheckConstraint("ck_evolutions_gender", "gender in ('male', 'female')");
                    table.CheckConstraint("ck_evolutions_time_of_day", "time_of_day in ('day', 'night')");
                    table.ForeignKey(
                        name: "fk_evolutions_forms_from_form_id",
                        column: x => x.from_form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evolutions_forms_to_form_id",
                        column: x => x.to_form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "form_moves",
                columns: table => new
                {
                    form_id = table.Column<string>(type: "text", nullable: false),
                    move_id = table.Column<string>(type: "text", nullable: false),
                    availability = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_moves", x => new { x.form_id, x.move_id });
                    table.ForeignKey(
                        name: "fk_form_moves_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_form_moves_moves_move_id",
                        column: x => x.move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "max_bosses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    stars = table.Column<int>(type: "integer", nullable: false),
                    is_gigantamax = table.Column<bool>(type: "boolean", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    time_zone = table.Column<string>(type: "text", nullable: true),
                    shiny_available = table.Column<bool>(type: "boolean", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    source_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    first_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_max_bosses", x => x.id);
                    table.CheckConstraint("ck_max_bosses_ends_after_start", "ends_at is null or ends_at > starts_at");
                    table.CheckConstraint("ck_max_bosses_stars", "stars between 1 and 6");
                    table.ForeignKey(
                        name: "fk_max_bosses_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_max_bosses_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_max_bosses_users_fixed_by_user_id",
                        column: x => x.fixed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "pvp_best_ivs",
                columns: table => new
                {
                    league = table.Column<string>(type: "text", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    attack_iv = table.Column<int>(type: "integer", nullable: false),
                    defense_iv = table.Column<int>(type: "integer", nullable: false),
                    stamina_iv = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    cp = table.Column<int>(type: "integer", nullable: false),
                    stat_product = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pvp_best_ivs", x => new { x.league, x.form_id });
                    table.CheckConstraint("ck_pvp_best_ivs_ivs", "attack_iv between 0 and 15 and defense_iv between 0 and 15 and stamina_iv between 0 and 15");
                    table.CheckConstraint("ck_pvp_best_ivs_level", "level between 1 and 51 and level * 2 = floor(level * 2)");
                    table.ForeignKey(
                        name: "fk_pvp_best_ivs_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pvp_rankings",
                columns: table => new
                {
                    league = table.Column<string>(type: "text", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    is_shadow = table.Column<bool>(type: "boolean", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    fast_move_id = table.Column<string>(type: "text", nullable: false),
                    charged_move1_id = table.Column<string>(type: "text", nullable: false),
                    charged_move2_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pvp_rankings", x => new { x.league, x.form_id, x.is_shadow });
                    table.CheckConstraint("ck_pvp_rankings_rank", "rank >= 1");
                    table.ForeignKey(
                        name: "fk_pvp_rankings_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pvp_rankings_moves_charged_move1_id",
                        column: x => x.charged_move1_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pvp_rankings_moves_charged_move2_id",
                        column: x => x.charged_move2_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pvp_rankings_moves_fast_move_id",
                        column: x => x.fast_move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "raid_bosses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    is_shadow = table.Column<bool>(type: "boolean", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    time_zone = table.Column<string>(type: "text", nullable: true),
                    shiny_available = table.Column<bool>(type: "boolean", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    source_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    first_fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fixed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_raid_bosses", x => x.id);
                    table.CheckConstraint("ck_raid_bosses_ends_after_start", "ends_at is null or ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_raid_bosses_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_raid_bosses_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_raid_bosses_users_fixed_by_user_id",
                        column: x => x.fixed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "raid_movesets",
                columns: table => new
                {
                    form_id = table.Column<string>(type: "text", nullable: false),
                    is_shadow = table.Column<bool>(type: "boolean", nullable: false),
                    fast_move_id = table.Column<string>(type: "text", nullable: false),
                    charged_move_id = table.Column<string>(type: "text", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    dps = table.Column<double>(type: "double precision", nullable: false),
                    tdo = table.Column<double>(type: "double precision", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_raid_movesets", x => new { x.form_id, x.is_shadow, x.fast_move_id, x.charged_move_id });
                    table.CheckConstraint("ck_raid_movesets_rank", "rank >= 1");
                    table.ForeignKey(
                        name: "fk_raid_movesets_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_raid_movesets_moves_charged_move_id",
                        column: x => x.charged_move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_raid_movesets_moves_fast_move_id",
                        column: x => x.fast_move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "raid_rankings",
                columns: table => new
                {
                    attacking_type_id = table.Column<string>(type: "text", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    is_shadow = table.Column<bool>(type: "boolean", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    fast_move_id = table.Column<string>(type: "text", nullable: false),
                    charged_move_id = table.Column<string>(type: "text", nullable: false),
                    dps = table.Column<double>(type: "double precision", nullable: false),
                    tdo = table.Column<double>(type: "double precision", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_raid_rankings", x => new { x.attacking_type_id, x.form_id, x.is_shadow });
                    table.CheckConstraint("ck_raid_rankings_rank", "rank >= 1");
                    table.ForeignKey(
                        name: "fk_raid_rankings_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_raid_rankings_moves_charged_move_id",
                        column: x => x.charged_move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_raid_rankings_moves_fast_move_id",
                        column: x => x.fast_move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_raid_rankings_types_attacking_type_id",
                        column: x => x.attacking_type_id,
                        principalTable: "types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "research_rewards",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    shiny_available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_research_rewards", x => new { x.task_id, x.form_id });
                    table.ForeignKey(
                        name: "fk_research_rewards_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_research_rewards_research_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "research_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    collection_pokemon_id = table.Column<Guid>(type: "uuid", nullable: true),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    what = table.Column<List<string>>(type: "text[]", nullable: false),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_changes", x => x.id);
                    table.CheckConstraint("ck_collection_changes_before_after", "(kind = 'New' and before is null and after is not null) or (kind = 'Gone' and before is not null and after is null) or (kind = 'Changed' and before is not null and after is not null)");
                    table.ForeignKey(
                        name: "fk_collection_changes_collection_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "collection_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_collection_changes_collection_pokemon_collection_pokemon_id",
                        column: x => x.collection_pokemon_id,
                        principalTable: "collection_pokemon",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_fixes_entry_table_entry_id",
                table: "admin_fixes",
                columns: new[] { "entry_table", "entry_id" });

            migrationBuilder.CreateIndex(
                name: "ix_admin_fixes_user_id",
                table: "admin_fixes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_at",
                table: "ai_usage",
                column: "at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_job_run_id",
                table: "ai_usage",
                column: "job_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_user_id_at",
                table: "ai_usage",
                columns: new[] { "user_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_collection_changes_collection_pokemon_id",
                table: "collection_changes",
                column: "collection_pokemon_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_changes_import_id_kind",
                table: "collection_changes",
                columns: new[] { "import_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_collection_imports_user_id_uploaded_at",
                table: "collection_imports",
                columns: new[] { "user_id", "uploaded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_collection_pokemon_charged_move1_id",
                table: "collection_pokemon",
                column: "charged_move1_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_pokemon_charged_move2_id",
                table: "collection_pokemon",
                column: "charged_move2_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_pokemon_fast_move_id",
                table: "collection_pokemon",
                column: "fast_move_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_pokemon_form_id",
                table: "collection_pokemon",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_pokemon_import_id",
                table: "collection_pokemon",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_pokemon_user_id_form_id",
                table: "collection_pokemon",
                columns: new[] { "user_id", "form_id" });

            migrationBuilder.CreateIndex(
                name: "ix_egg_pool_entries_egg",
                table: "egg_pool_entries",
                column: "egg");

            migrationBuilder.CreateIndex(
                name: "ix_egg_pool_entries_event_id",
                table: "egg_pool_entries",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_egg_pool_entries_fixed_by_user_id",
                table: "egg_pool_entries",
                column: "fixed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_egg_pool_entries_form_id",
                table: "egg_pool_entries",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_egg_pool_entries_key",
                table: "egg_pool_entries",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_moves_form_id",
                table: "event_moves",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_moves_move_id",
                table: "event_moves",
                column: "move_id");

            migrationBuilder.CreateIndex(
                name: "ix_event_pokemon_form_id",
                table: "event_pokemon",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_fixed_by_user_id",
                table: "events",
                column: "fixed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_events_key",
                table: "events",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_starts_at_ends_at",
                table: "events",
                columns: new[] { "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_evolutions_from_form_id",
                table: "evolutions",
                column: "from_form_id");

            migrationBuilder.CreateIndex(
                name: "ix_evolutions_to_form_id",
                table: "evolutions",
                column: "to_form_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_moves_move_id",
                table: "form_moves",
                column: "move_id");

            migrationBuilder.CreateIndex(
                name: "ix_forms_base_form_id",
                table: "forms",
                column: "base_form_id");

            migrationBuilder.CreateIndex(
                name: "ix_forms_default_per_species",
                table: "forms",
                column: "dex",
                unique: true,
                filter: "is_default");

            migrationBuilder.CreateIndex(
                name: "ix_forms_dex_id",
                table: "forms",
                columns: new[] { "dex", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_forms_game_master_id",
                table: "forms",
                column: "game_master_id");

            migrationBuilder.CreateIndex(
                name: "ix_forms_type1_id",
                table: "forms",
                column: "type1_id");

            migrationBuilder.CreateIndex(
                name: "ix_forms_type2_id",
                table: "forms",
                column: "type2_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_job_started_at",
                table: "job_runs",
                columns: new[] { "job", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_job_runs_requested_by_user_id",
                table: "job_runs",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_max_bosses_event_id",
                table: "max_bosses",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_max_bosses_fixed_by_user_id",
                table: "max_bosses",
                column: "fixed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_max_bosses_form_id",
                table: "max_bosses",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_max_bosses_key",
                table: "max_bosses",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_max_bosses_starts_at_ends_at",
                table: "max_bosses",
                columns: new[] { "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_moves_type_id",
                table: "moves",
                column: "type_id");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_best_ivs_form_id",
                table: "pvp_best_ivs",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_keeper_boxes_dex",
                table: "pvp_keeper_boxes",
                column: "dex");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_rankings_charged_move1_id",
                table: "pvp_rankings",
                column: "charged_move1_id");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_rankings_charged_move2_id",
                table: "pvp_rankings",
                column: "charged_move2_id");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_rankings_fast_move_id",
                table: "pvp_rankings",
                column: "fast_move_id");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_rankings_form_id",
                table: "pvp_rankings",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_rankings_league_rank",
                table: "pvp_rankings",
                columns: new[] { "league", "rank" });

            migrationBuilder.CreateIndex(
                name: "ix_raid_bosses_event_id",
                table: "raid_bosses",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_raid_bosses_fixed_by_user_id",
                table: "raid_bosses",
                column: "fixed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_raid_bosses_form_id",
                table: "raid_bosses",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_raid_bosses_key",
                table: "raid_bosses",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_raid_bosses_starts_at_ends_at",
                table: "raid_bosses",
                columns: new[] { "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_raid_movesets_charged_move_id",
                table: "raid_movesets",
                column: "charged_move_id");

            migrationBuilder.CreateIndex(
                name: "ix_raid_movesets_fast_move_id",
                table: "raid_movesets",
                column: "fast_move_id");

            migrationBuilder.CreateIndex(
                name: "ix_raid_rankings_attacking_type_id_rank",
                table: "raid_rankings",
                columns: new[] { "attacking_type_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "ix_raid_rankings_charged_move_id",
                table: "raid_rankings",
                column: "charged_move_id");

            migrationBuilder.CreateIndex(
                name: "ix_raid_rankings_fast_move_id",
                table: "raid_rankings",
                column: "fast_move_id");

            migrationBuilder.CreateIndex(
                name: "ix_raid_rankings_form_id",
                table: "raid_rankings",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_research_rewards_form_id",
                table: "research_rewards",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_research_tasks_event_id",
                table: "research_tasks",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_research_tasks_fixed_by_user_id",
                table: "research_tasks",
                column: "fixed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_research_tasks_key",
                table: "research_tasks",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_filters_user_id",
                table: "saved_filters",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_species_family_id",
                table: "species",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_types_sort_order",
                table: "types",
                column: "sort_order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_google_subject",
                table: "users",
                column: "google_subject",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_fixes");

            migrationBuilder.DropTable(
                name: "ai_usage");

            migrationBuilder.DropTable(
                name: "collection_changes");

            migrationBuilder.DropTable(
                name: "cp_multipliers");

            migrationBuilder.DropTable(
                name: "egg_pool_entries");

            migrationBuilder.DropTable(
                name: "event_bonuses");

            migrationBuilder.DropTable(
                name: "event_moves");

            migrationBuilder.DropTable(
                name: "event_pokemon");

            migrationBuilder.DropTable(
                name: "evolutions");

            migrationBuilder.DropTable(
                name: "form_moves");

            migrationBuilder.DropTable(
                name: "keeper_filters");

            migrationBuilder.DropTable(
                name: "max_bosses");

            migrationBuilder.DropTable(
                name: "pvp_best_ivs");

            migrationBuilder.DropTable(
                name: "pvp_keeper_boxes");

            migrationBuilder.DropTable(
                name: "pvp_rankings");

            migrationBuilder.DropTable(
                name: "raid_bosses");

            migrationBuilder.DropTable(
                name: "raid_movesets");

            migrationBuilder.DropTable(
                name: "raid_rankings");

            migrationBuilder.DropTable(
                name: "research_rewards");

            migrationBuilder.DropTable(
                name: "saved_filters");

            migrationBuilder.DropTable(
                name: "unrecognised_rows");

            migrationBuilder.DropTable(
                name: "job_runs");

            migrationBuilder.DropTable(
                name: "collection_pokemon");

            migrationBuilder.DropTable(
                name: "research_tasks");

            migrationBuilder.DropTable(
                name: "collection_imports");

            migrationBuilder.DropTable(
                name: "forms");

            migrationBuilder.DropTable(
                name: "moves");

            migrationBuilder.DropTable(
                name: "events");

            migrationBuilder.DropTable(
                name: "species");

            migrationBuilder.DropTable(
                name: "types");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "families");
        }
    }
}
