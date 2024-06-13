using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class mssqllocal_migration_736 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ID",
                table: "Appointments",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_ID",
                table: "Appointments",
                newName: "IX_Appointments_Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Appointments",
                newName: "ID");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_Id",
                table: "Appointments",
                newName: "IX_Appointments_ID");
        }
    }
}
