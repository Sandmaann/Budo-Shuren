using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class NeuigkeitenAblaufdatumUndStandard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Ablaufdatum",
                table: "Neuigkeiten",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IstStandardneuigkeit",
                table: "Neuigkeiten",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ablaufdatum",
                table: "Neuigkeiten");

            migrationBuilder.DropColumn(
                name: "IstStandardneuigkeit",
                table: "Neuigkeiten");
        }
    }
}
