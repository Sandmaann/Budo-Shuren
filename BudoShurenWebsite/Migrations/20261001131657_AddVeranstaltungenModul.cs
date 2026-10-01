using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudoShurenWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddVeranstaltungenModul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Veranstaltungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Kurzbeschreibung = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Beschreibung = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BildId = table.Column<int>(type: "int", nullable: true),
                    Ort = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Adresse = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    KartenLink = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AbteilungId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    KontaktName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KontaktEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Sichtbarkeit = table.Column<int>(type: "int", nullable: false),
                    ErstmalsVeroeffentlichtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnmeldungAb = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AnmeldungBis = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AenderungenBis = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Teilnahmemodus = table.Column<int>(type: "int", nullable: false),
                    MinTageBeiTeilanmeldung = table.Column<int>(type: "int", nullable: false),
                    MaxTeilnehmerVorgabe = table.Column<int>(type: "int", nullable: true),
                    MaxBegleitpersonen = table.Column<int>(type: "int", nullable: false),
                    DoubleOptIn = table.Column<bool>(type: "bit", nullable: false),
                    TelefonFeld = table.Column<int>(type: "int", nullable: false),
                    VereinFeld = table.Column<int>(type: "int", nullable: false),
                    GraduierungFeld = table.Column<int>(type: "int", nullable: false),
                    BemerkungFeld = table.Column<int>(type: "int", nullable: false),
                    ZusammenfassungUhrzeit = table.Column<TimeOnly>(type: "time", nullable: false),
                    ErstelltVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ErstelltUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeaendertVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GeaendertUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veranstaltungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Veranstaltungen_Abteilungen_AbteilungId",
                        column: x => x.AbteilungId,
                        principalTable: "Abteilungen",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Veranstaltungen_Images_BildId",
                        column: x => x.BildId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Anmeldungen",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VeranstaltungId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Vorname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nachname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Telefon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Verein = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Graduierung = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Bemerkung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AnzahlBegleitpersonen = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusGrund = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Quelle = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    TokenErstelltUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NeueEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    NeueEmailTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    ReserviertBisUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailBestaetigtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DatenschutzAkzeptiertUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdminNotiz = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AdminGesehenUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErstelltUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeaendertUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anmeldungen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anmeldungen_Veranstaltungen_VeranstaltungId",
                        column: x => x.VeranstaltungId,
                        principalTable: "Veranstaltungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BenachrichtigungEmpfaenger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VeranstaltungId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ereignisse = table.Column<int>(type: "int", nullable: false),
                    Modus = table.Column<int>(type: "int", nullable: false),
                    AbmeldeTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: true),
                    AbgemeldetUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BenachrichtigtBisUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HinzugefuegtVon = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ErstelltUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BenachrichtigungEmpfaenger", x => x.Id);
                    table.CheckConstraint("CK_BenachrichtigungEmpfaenger_UserIdOderEmail", "(CASE WHEN [UserId] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [Email] IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_BenachrichtigungEmpfaenger_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BenachrichtigungEmpfaenger_Veranstaltungen_VeranstaltungId",
                        column: x => x.VeranstaltungId,
                        principalTable: "Veranstaltungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VeranstaltungsTage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VeranstaltungId = table.Column<int>(type: "int", nullable: false),
                    Datum = table.Column<DateOnly>(type: "date", nullable: false),
                    Beginn = table.Column<TimeOnly>(type: "time", nullable: false),
                    Ende = table.Column<TimeOnly>(type: "time", nullable: false),
                    Titel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MaxTeilnehmer = table.Column<int>(type: "int", nullable: true),
                    Abgesagt = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeranstaltungsTage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeranstaltungsTage_Veranstaltungen_VeranstaltungId",
                        column: x => x.VeranstaltungId,
                        principalTable: "Veranstaltungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnmeldungEreignisse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnmeldungId = table.Column<int>(type: "int", nullable: false),
                    ZeitpunktUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Akteur = table.Column<int>(type: "int", nullable: false),
                    AkteurUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Art = table.Column<int>(type: "int", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnmeldungEreignisse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnmeldungEreignisse_Anmeldungen_AnmeldungId",
                        column: x => x.AnmeldungId,
                        principalTable: "Anmeldungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnmeldungInfoEmails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnmeldungId = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    AbmeldeTokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    AbgemeldetUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnmeldungInfoEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnmeldungInfoEmails_Anmeldungen_AnmeldungId",
                        column: x => x.AnmeldungId,
                        principalTable: "Anmeldungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnmeldungTage",
                columns: table => new
                {
                    AnmeldungId = table.Column<int>(type: "int", nullable: false),
                    VeranstaltungsTagId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnmeldungTage", x => new { x.AnmeldungId, x.VeranstaltungsTagId });
                    table.ForeignKey(
                        name: "FK_AnmeldungTage_Anmeldungen_AnmeldungId",
                        column: x => x.AnmeldungId,
                        principalTable: "Anmeldungen",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnmeldungTage_VeranstaltungsTage_VeranstaltungsTagId",
                        column: x => x.VeranstaltungsTagId,
                        principalTable: "VeranstaltungsTage",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anmeldungen_NeueEmailTokenHash",
                table: "Anmeldungen",
                column: "NeueEmailTokenHash",
                unique: true,
                filter: "[NeueEmailTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Anmeldungen_TokenHash",
                table: "Anmeldungen",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Anmeldungen_VeranstaltungId_Email",
                table: "Anmeldungen",
                columns: new[] { "VeranstaltungId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnmeldungEreignisse_AnmeldungId_ZeitpunktUtc",
                table: "AnmeldungEreignisse",
                columns: new[] { "AnmeldungId", "ZeitpunktUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AnmeldungInfoEmails_AbmeldeTokenHash",
                table: "AnmeldungInfoEmails",
                column: "AbmeldeTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnmeldungInfoEmails_AnmeldungId_Email",
                table: "AnmeldungInfoEmails",
                columns: new[] { "AnmeldungId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnmeldungTage_VeranstaltungsTagId",
                table: "AnmeldungTage",
                column: "VeranstaltungsTagId");

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungEmpfaenger_AbmeldeTokenHash",
                table: "BenachrichtigungEmpfaenger",
                column: "AbmeldeTokenHash",
                unique: true,
                filter: "[AbmeldeTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungEmpfaenger_UserId",
                table: "BenachrichtigungEmpfaenger",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungEmpfaenger_VeranstaltungId_Email",
                table: "BenachrichtigungEmpfaenger",
                columns: new[] { "VeranstaltungId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BenachrichtigungEmpfaenger_VeranstaltungId_UserId",
                table: "BenachrichtigungEmpfaenger",
                columns: new[] { "VeranstaltungId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Veranstaltungen_AbteilungId",
                table: "Veranstaltungen",
                column: "AbteilungId");

            migrationBuilder.CreateIndex(
                name: "IX_Veranstaltungen_BildId",
                table: "Veranstaltungen",
                column: "BildId");

            migrationBuilder.CreateIndex(
                name: "IX_Veranstaltungen_Slug",
                table: "Veranstaltungen",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VeranstaltungsTage_VeranstaltungId_Datum",
                table: "VeranstaltungsTage",
                columns: new[] { "VeranstaltungId", "Datum" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnmeldungEreignisse");

            migrationBuilder.DropTable(
                name: "AnmeldungInfoEmails");

            migrationBuilder.DropTable(
                name: "AnmeldungTage");

            migrationBuilder.DropTable(
                name: "BenachrichtigungEmpfaenger");

            migrationBuilder.DropTable(
                name: "Anmeldungen");

            migrationBuilder.DropTable(
                name: "VeranstaltungsTage");

            migrationBuilder.DropTable(
                name: "Veranstaltungen");
        }
    }
}
