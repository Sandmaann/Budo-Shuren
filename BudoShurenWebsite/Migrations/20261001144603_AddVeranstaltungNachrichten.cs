using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddVeranstaltungNachrichten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VeranstaltungNachrichten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VeranstaltungId = table.Column<int>(type: "int", nullable: false),
                    Art = table.Column<int>(type: "int", nullable: false),
                    Betreff = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InhaltMarkdown = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnInfoAdressen = table.Column<bool>(type: "bit", nullable: false),
                    AnzahlEmpfaenger = table.Column<int>(type: "int", nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GesendetUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeranstaltungNachrichten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeranstaltungNachrichten_Veranstaltungen_VeranstaltungId",
                        column: x => x.VeranstaltungId,
                        principalTable: "Veranstaltungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VeranstaltungNachrichten_VeranstaltungId_GesendetUtc",
                table: "VeranstaltungNachrichten",
                columns: new[] { "VeranstaltungId", "GesendetUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VeranstaltungNachrichten");
        }
    }
}
