using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddAktuellesModul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AktuellesBeitraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    AbteilungId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Veroeffentlicht = table.Column<bool>(type: "bit", nullable: false),
                    Datum = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MetaTitel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetaBeschreibung = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Erstellt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Geaendert = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AktuellesBeitraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AktuellesBeitraege_Abteilungen_AbteilungId",
                        column: x => x.AbteilungId,
                        principalTable: "Abteilungen",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AktuellesBloecke",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BeitragId = table.Column<int>(type: "int", nullable: false),
                    Typ = table.Column<int>(type: "int", nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
                    MarkdownInhalt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BilderProReihe = table.Column<int>(type: "int", nullable: false),
                    BildUnterschrift = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AktuellesBloecke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AktuellesBloecke_AktuellesBeitraege_BeitragId",
                        column: x => x.BeitragId,
                        principalTable: "AktuellesBeitraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AktuellesBilder",
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
                    table.PrimaryKey("PK_AktuellesBilder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AktuellesBilder_AktuellesBloecke_BlockId",
                        column: x => x.BlockId,
                        principalTable: "AktuellesBloecke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AktuellesBilder_Images_BildId",
                        column: x => x.BildId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBeitraege_AbteilungId",
                table: "AktuellesBeitraege",
                column: "AbteilungId");

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBeitraege_Id",
                table: "AktuellesBeitraege",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBeitraege_Slug",
                table: "AktuellesBeitraege",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBilder_BildId",
                table: "AktuellesBilder",
                column: "BildId");

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBilder_BlockId",
                table: "AktuellesBilder",
                column: "BlockId");

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBilder_Id",
                table: "AktuellesBilder",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBloecke_BeitragId",
                table: "AktuellesBloecke",
                column: "BeitragId");

            migrationBuilder.CreateIndex(
                name: "IX_AktuellesBloecke_Id",
                table: "AktuellesBloecke",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AktuellesBilder");

            migrationBuilder.DropTable(
                name: "AktuellesBloecke");

            migrationBuilder.DropTable(
                name: "AktuellesBeitraege");
        }
    }
}
