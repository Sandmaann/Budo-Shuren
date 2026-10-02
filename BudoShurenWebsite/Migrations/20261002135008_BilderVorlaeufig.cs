using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class BilderVorlaeufig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VorlaeufigSeitUtc",
                table: "Images",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_VorlaeufigSeitUtc",
                table: "Images",
                column: "VorlaeufigSeitUtc",
                filter: "[VorlaeufigSeitUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Images_VorlaeufigSeitUtc",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "VorlaeufigSeitUtc",
                table: "Images");
        }
    }
}
