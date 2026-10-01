using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAusgang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailAusgang",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    An = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    AntwortAn = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Betreff = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Html = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AnhaengeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Prioritaet = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Versuche = table.Column<int>(type: "int", nullable: false),
                    LetzterFehler = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ErstelltUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FaelligAbUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GesendetAmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BezugTyp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BezugId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAusgang", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAusgang_Status_Prioritaet_FaelligAbUtc",
                table: "EmailAusgang",
                columns: new[] { "Status", "Prioritaet", "FaelligAbUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAusgang");
        }
    }
}
