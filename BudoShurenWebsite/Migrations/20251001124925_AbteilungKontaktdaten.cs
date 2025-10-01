using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AbteilungKontaktdaten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kontaktperson",
                table: "Abteilungen",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Abteilungen",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Telefon",
                table: "Abteilungen",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kontaktperson",
                table: "Abteilungen");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Abteilungen");

            migrationBuilder.DropColumn(
                name: "Telefon",
                table: "Abteilungen");
        }
    }
}
