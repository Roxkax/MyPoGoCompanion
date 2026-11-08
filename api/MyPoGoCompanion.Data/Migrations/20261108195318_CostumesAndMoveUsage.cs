using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyPoGoCompanion.Data.Migrations
{
    /// <inheritdoc />
    public partial class CostumesAndMoveUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "costumes",
                columns: table => new
                {
                    sprite = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dex = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    form_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    name_en = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    has_shiny_sprite = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_costumes", x => x.sprite);
                    table.ForeignKey(
                        name: "fk_costumes_species_dex",
                        column: x => x.dex,
                        principalTable: "species",
                        principalColumn: "dex",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pvp_move_usage",
                columns: table => new
                {
                    league = table.Column<string>(type: "text", nullable: false),
                    form_id = table.Column<string>(type: "text", nullable: false),
                    is_shadow = table.Column<bool>(type: "boolean", nullable: false),
                    move_id = table.Column<string>(type: "text", nullable: false),
                    uses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pvp_move_usage", x => new { x.league, x.form_id, x.is_shadow, x.move_id });
                    table.CheckConstraint("ck_pvp_move_usage_uses", "uses >= 0");
                    table.ForeignKey(
                        name: "fk_pvp_move_usage_forms_form_id",
                        column: x => x.form_id,
                        principalTable: "forms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_pvp_move_usage_moves_move_id",
                        column: x => x.move_id,
                        principalTable: "moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_costumes_dex",
                table: "costumes",
                column: "dex");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_move_usage_form_id",
                table: "pvp_move_usage",
                column: "form_id");

            migrationBuilder.CreateIndex(
                name: "ix_pvp_move_usage_move_id",
                table: "pvp_move_usage",
                column: "move_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "costumes");

            migrationBuilder.DropTable(
                name: "pvp_move_usage");
        }
    }
}
