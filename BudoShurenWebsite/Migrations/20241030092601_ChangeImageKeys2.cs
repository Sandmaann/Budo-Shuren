using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class ChangeImageKeys2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Galerie_GalerieEintragId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_GalerieEintragId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "GalerieEintragId",
                table: "Images");

            migrationBuilder.AddColumn<int>(
                name: "DbImageId",
                table: "Galerie",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Galerie_DbImageId",
                table: "Galerie",
                column: "DbImageId",
                unique: true,
                filter: "[DbImageId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Galerie_Images_DbImageId",
                table: "Galerie",
                column: "DbImageId",
                principalTable: "Images",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Galerie_Images_DbImageId",
                table: "Galerie");

            migrationBuilder.DropIndex(
                name: "IX_Galerie_DbImageId",
                table: "Galerie");

            migrationBuilder.DropColumn(
                name: "DbImageId",
                table: "Galerie");

            migrationBuilder.AddColumn<int>(
                name: "GalerieEintragId",
                table: "Images",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_GalerieEintragId",
                table: "Images",
                column: "GalerieEintragId");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Galerie_GalerieEintragId",
                table: "Images",
                column: "GalerieEintragId",
                principalTable: "Galerie",
                principalColumn: "ID");
        }
    }
}
