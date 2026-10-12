using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class KontaktAnfragen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KontaktAnfragen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ZeitpunktUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Abteilung = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AbsenderEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Nachricht = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Hinweis = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AnzahlMails = table.Column<int>(type: "int", nullable: false),
                    Seite = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Interaktiv = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KontaktAnfragen", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KontaktAnfragen_ZeitpunktUtc",
                table: "KontaktAnfragen",
                column: "ZeitpunktUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KontaktAnfragen");
        }
    }
}
