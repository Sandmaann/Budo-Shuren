using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkStuff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AbteilungLink",
                table: "WissenBeitraege",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbteilungLink",
                table: "WissenBeitraege");
        }
    }
}
