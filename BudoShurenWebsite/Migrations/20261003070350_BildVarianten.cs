using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class BildVarianten : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Breite",
                table: "Images",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Hoehe",
                table: "Images",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BildVarianten",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BildId = table.Column<int>(type: "int", nullable: false),
                    Art = table.Column<int>(type: "int", nullable: false),
                    Breite = table.Column<int>(type: "int", nullable: false),
                    Hoehe = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Daten = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ErstelltUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BildVarianten", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BildVarianten_Images_BildId",
                        column: x => x.BildId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BildVarianten_BildId_Art",
                table: "BildVarianten",
                columns: new[] { "BildId", "Art" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BildVarianten");

            migrationBuilder.DropColumn(
                name: "Breite",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "Hoehe",
                table: "Images");
        }
    }
}
