using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddKalenderVeranstaltungsTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VeranstaltungsTagId",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_VeranstaltungsTagId",
                table: "Appointments",
                column: "VeranstaltungsTagId",
                unique: true,
                filter: "[VeranstaltungsTagId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_VeranstaltungsTage_VeranstaltungsTagId",
                table: "Appointments",
                column: "VeranstaltungsTagId",
                principalTable: "VeranstaltungsTage",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_VeranstaltungsTage_VeranstaltungsTagId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_VeranstaltungsTagId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "VeranstaltungsTagId",
                table: "Appointments");
        }
    }
}
