using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class MehrereTermineProTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VeranstaltungsTage_VeranstaltungId_Datum",
                table: "VeranstaltungsTage");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "Ende",
                table: "VeranstaltungsTage",
                type: "time",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time");

            migrationBuilder.CreateIndex(
                name: "IX_VeranstaltungsTage_VeranstaltungId_Datum_Beginn",
                table: "VeranstaltungsTage",
                columns: new[] { "VeranstaltungId", "Datum", "Beginn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VeranstaltungsTage_VeranstaltungId_Datum_Beginn",
                table: "VeranstaltungsTage");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "Ende",
                table: "VeranstaltungsTage",
                type: "time",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VeranstaltungsTage_VeranstaltungId_Datum",
                table: "VeranstaltungsTage",
                columns: new[] { "VeranstaltungId", "Datum" },
                unique: true);
        }
    }
}
