using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddWissenBlockBannerUndText2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BannerAusblenden",
                table: "WissenBloecke",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BannerTitel",
                table: "WissenBloecke",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextInhalt2",
                table: "WissenBloecke",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerAusblenden",
                table: "WissenBloecke");

            migrationBuilder.DropColumn(
                name: "BannerTitel",
                table: "WissenBloecke");

            migrationBuilder.DropColumn(
                name: "TextInhalt2",
                table: "WissenBloecke");
        }
    }
}
