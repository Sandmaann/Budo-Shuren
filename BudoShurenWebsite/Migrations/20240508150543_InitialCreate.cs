using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Galerie",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Untertitel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Beschreibung = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Home = table.Column<bool>(type: "bit", nullable: false),
                    Aikido = table.Column<bool>(type: "bit", nullable: false),
                    Bujinkan = table.Column<bool>(type: "bit", nullable: false),
                    Genbukan = table.Column<bool>(type: "bit", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ImageCreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntryCreationDateUTC = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EntryCreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Galerie", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Galerie_ID",
                table: "Galerie",
                column: "ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Galerie");
        }
    }
}
