using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddWissensbereich : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WissenKategorien",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AbteilungId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WissenKategorien", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WissenKategorien_Abteilungen_AbteilungId",
                        column: x => x.AbteilungId,
                        principalTable: "Abteilungen",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WissenBeitraege",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    KategorieId = table.Column<int>(type: "int", nullable: false),
                    MetaTitel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MetaBeschreibung = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Veroeffentlicht = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Erstellt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GeaendertVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Geaendert = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WissenBeitraege", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WissenBeitraege_WissenKategorien_KategorieId",
                        column: x => x.KategorieId,
                        principalTable: "WissenKategorien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WissenBloecke",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BeitragId = table.Column<int>(type: "int", nullable: false),
                    Typ = table.Column<int>(type: "int", nullable: false),
                    Sortierung = table.Column<int>(type: "int", nullable: false),
                    TextInhalt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UntertitelText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BildId = table.Column<int>(type: "int", nullable: true),
                    AltText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BildPosition = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ListenItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WissenBloecke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WissenBloecke_Images_BildId",
                        column: x => x.BildId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WissenBloecke_WissenBeitraege_BeitragId",
                        column: x => x.BeitragId,
                        principalTable: "WissenBeitraege",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "WissenKategorien",
                columns: new[] { "Id", "AbteilungId", "Name", "Slug", "SortOrder" },
                values: new object[] { 1, null, "Allgemein", "allgemein", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_WissenBeitraege_Id",
                table: "WissenBeitraege",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WissenBeitraege_KategorieId",
                table: "WissenBeitraege",
                column: "KategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_WissenBeitraege_Slug",
                table: "WissenBeitraege",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WissenBloecke_BeitragId",
                table: "WissenBloecke",
                column: "BeitragId");

            migrationBuilder.CreateIndex(
                name: "IX_WissenBloecke_BildId",
                table: "WissenBloecke",
                column: "BildId");

            migrationBuilder.CreateIndex(
                name: "IX_WissenBloecke_Id",
                table: "WissenBloecke",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WissenKategorien_AbteilungId",
                table: "WissenKategorien",
                column: "AbteilungId");

            migrationBuilder.CreateIndex(
                name: "IX_WissenKategorien_Id",
                table: "WissenKategorien",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WissenBloecke");

            migrationBuilder.DropTable(
                name: "WissenBeitraege");

            migrationBuilder.DropTable(
                name: "WissenKategorien");
        }
    }
}
