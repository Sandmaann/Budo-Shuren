using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class ChangeImageKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DbImageId",
                table: "Neuigkeiten",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GalerieEintragId",
                table: "Images",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Neuigkeiten_DbImageId",
                table: "Neuigkeiten",
                column: "DbImageId",
                unique: true,
                filter: "[DbImageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Images_GalerieEintragId",
                table: "Images",
                column: "GalerieEintragId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_Id",
                table: "Images",
                column: "Id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Galerie_GalerieEintragId",
                table: "Images",
                column: "GalerieEintragId",
                principalTable: "Galerie",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Neuigkeiten_Images_DbImageId",
                table: "Neuigkeiten",
                column: "DbImageId",
                principalTable: "Images",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Galerie_GalerieEintragId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Neuigkeiten_Images_DbImageId",
                table: "Neuigkeiten");

            migrationBuilder.DropIndex(
                name: "IX_Neuigkeiten_DbImageId",
                table: "Neuigkeiten");

            migrationBuilder.DropIndex(
                name: "IX_Images_GalerieEintragId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_Id",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "DbImageId",
                table: "Neuigkeiten");

            migrationBuilder.DropColumn(
                name: "GalerieEintragId",
                table: "Images");
        }
    }
}
