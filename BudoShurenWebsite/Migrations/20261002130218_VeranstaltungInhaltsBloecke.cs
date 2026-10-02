using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class VeranstaltungInhaltsBloecke : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Veranstaltungen_Images_BildId",
                table: "Veranstaltungen");

            migrationBuilder.DropIndex(
                name: "IX_Veranstaltungen_BildId",
                table: "Veranstaltungen");

            migrationBuilder.DropColumn(
                name: "BildId",
                table: "Veranstaltungen");

            migrationBuilder.CreateTable(
                name: "VeranstaltungBloecke",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VeranstaltungId = table.Column<int>(type: "int", nullable: false),
                    Typ = table.Column<int>(type: "int", nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
                    MarkdownInhalt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BilderProReihe = table.Column<int>(type: "int", nullable: false),
                    BildUnterschrift = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeranstaltungBloecke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeranstaltungBloecke_Veranstaltungen_VeranstaltungId",
                        column: x => x.VeranstaltungId,
                        principalTable: "Veranstaltungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VeranstaltungBilder",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BlockId = table.Column<int>(type: "int", nullable: false),
                    BildId = table.Column<int>(type: "int", nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeranstaltungBilder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeranstaltungBilder_Images_BildId",
                        column: x => x.BildId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VeranstaltungBilder_VeranstaltungBloecke_BlockId",
                        column: x => x.BlockId,
                        principalTable: "VeranstaltungBloecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VeranstaltungBilder_BildId",
                table: "VeranstaltungBilder",
                column: "BildId");

            migrationBuilder.CreateIndex(
                name: "IX_VeranstaltungBilder_BlockId",
                table: "VeranstaltungBilder",
                column: "BlockId");

            migrationBuilder.CreateIndex(
                name: "IX_VeranstaltungBloecke_VeranstaltungId",
                table: "VeranstaltungBloecke",
                column: "VeranstaltungId");

            // Bisherige Beschreibung wird der erste Textbaustein (Typ 0 = MarkdownText)
            migrationBuilder.Sql("""
                INSERT INTO VeranstaltungBloecke (VeranstaltungId, Typ, Sortierung, MarkdownInhalt, BilderProReihe)
                SELECT Id, 0, 0, Beschreibung, 3 FROM Veranstaltungen WHERE LTRIM(RTRIM(Beschreibung)) <> ''
                """);

            migrationBuilder.DropColumn(
                name: "Beschreibung",
                table: "Veranstaltungen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Beschreibung",
                table: "Veranstaltungen",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Der erste Textbaustein wird wieder die Beschreibung (weitere Bausteine und Bilder gehen verloren)
            migrationBuilder.Sql("""
                UPDATE v SET Beschreibung = ISNULL((
                    SELECT TOP 1 b.MarkdownInhalt FROM VeranstaltungBloecke b
                    WHERE b.VeranstaltungId = v.Id AND b.Typ = 0 ORDER BY b.Sortierung), '')
                FROM Veranstaltungen v
                """);

            migrationBuilder.DropTable(
                name: "VeranstaltungBilder");

            migrationBuilder.DropTable(
                name: "VeranstaltungBloecke");

            migrationBuilder.AddColumn<int>(
                name: "BildId",
                table: "Veranstaltungen",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veranstaltungen_BildId",
                table: "Veranstaltungen",
                column: "BildId");

            migrationBuilder.AddForeignKey(
                name: "FK_Veranstaltungen_Images_BildId",
                table: "Veranstaltungen",
                column: "BildId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
