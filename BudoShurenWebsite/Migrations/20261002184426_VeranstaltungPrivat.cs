using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class VeranstaltungPrivat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrivateVeranstaltung",
                table: "Veranstaltungen",
                type: "bit",
                nullable: false,
                // Bestehende Veranstaltungen bekommen den Hinweis wie neue (Standard an)
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrivateVeranstaltung",
                table: "Veranstaltungen");
        }
    }
}
