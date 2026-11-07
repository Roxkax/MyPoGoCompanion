using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyPoGoCompanion.Data.Migrations
{
    /// <inheritdoc />
    public partial class FormSprites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sprite",
                table: "forms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sprite_gigantamax",
                table: "forms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sprite_shiny",
                table: "forms",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sprite",
                table: "forms");

            migrationBuilder.DropColumn(
                name: "sprite_gigantamax",
                table: "forms");

            migrationBuilder.DropColumn(
                name: "sprite_shiny",
                table: "forms");
        }
    }
}
