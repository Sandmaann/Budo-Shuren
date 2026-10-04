using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class BearbeitungsEntwuerfe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BearbeitungsEntwuerfe",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BenutzerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Schluessel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Daten = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeaendertUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BearbeitungsEntwuerfe", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BearbeitungsEntwuerfe_AspNetUsers_BenutzerId",
                        column: x => x.BenutzerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BearbeitungsEntwuerfe_BenutzerId_Schluessel",
                table: "BearbeitungsEntwuerfe",
                columns: new[] { "BenutzerId", "Schluessel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BearbeitungsEntwuerfe");
        }
    }
}
