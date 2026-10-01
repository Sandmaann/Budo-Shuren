using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>
    /// Eine Veranstaltung (z. B. Seminar), für die sich Gäste ohne Benutzerkonto anmelden können.
    /// Zeitangaben ohne "Utc" im Namen sind Ortszeit Europe/Berlin (siehe Global/Ortszeit).
    /// </summary>
    public class Veranstaltung
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Titel { get; set; } = string.Empty;

        /// <summary>Teil der öffentlichen URL. Nach der ersten Veröffentlichung nicht mehr änderbar (verschickte Links).</summary>
        [Required]
        [MaxLength(250)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Kurzbeschreibung { get; set; }

        /// <summary>Markdown, gerendert mit Markdig.</summary>
        public string Beschreibung { get; set; } = string.Empty;

        public int? BildId { get; set; }

        public DbImage? Bild { get; set; }

        [MaxLength(200)]
        public string? Ort { get; set; }

        [MaxLength(300)]
        public string? Adresse { get; set; }

        [MaxLength(500)]
        public string? KartenLink { get; set; }

        [MaxLength(450)]
        public string? AbteilungId { get; set; }

        public Abteilung? Abteilung { get; set; }

        [MaxLength(200)]
        public string? KontaktName { get; set; }

        /// <summary>Reply-To für alle Mails an Teilnehmer. Pflicht vor dem Veröffentlichen.</summary>
        [MaxLength(320)]
        public string? KontaktEmail { get; set; }

        public VeranstaltungStatus Status { get; set; } = VeranstaltungStatus.Entwurf;

        public VeranstaltungSichtbarkeit Sichtbarkeit { get; set; } = VeranstaltungSichtbarkeit.Oeffentlich;

        /// <summary>Gesetzt bei der ersten Veröffentlichung; danach ist der Slug gesperrt.</summary>
        public DateTime? ErstmalsVeroeffentlichtUtc { get; set; }

        /// <summary>Ortszeit. Leer = Anmeldung ab Veröffentlichung möglich.</summary>
        public DateTime? AnmeldungAb { get; set; }

        /// <summary>Ortszeit. Leer = Anmeldung bis zum Beginn möglich.</summary>
        public DateTime? AnmeldungBis { get; set; }

        /// <summary>Ortszeit. Danach können Teilnehmer nur noch ansehen und sich abmelden. Leer = bis zum Beginn.</summary>
        public DateTime? AenderungenBis { get; set; }

        public Teilnahmemodus Teilnahmemodus { get; set; } = Teilnahmemodus.NurGesamt;

        /// <summary>Nur bei Teilnahmemodus EinzelneTage: so viele Tage muss eine Anmeldung mindestens umfassen.</summary>
        public int MinTageBeiTeilanmeldung { get; set; } = 1;

        /// <summary>Vorgabe für die Kapazität neuer Tage. Maßgeblich ist VeranstaltungsTag.MaxTeilnehmer.</summary>
        public int? MaxTeilnehmerVorgabe { get; set; }

        public int MaxBegleitpersonen { get; set; } = 2;

        /// <summary>Anmeldung erst nach Klick auf den Bestätigungslink gültig.</summary>
        public bool DoubleOptIn { get; set; } = true;

        public FormularFeldModus TelefonFeld { get; set; } = FormularFeldModus.Optional;

        public FormularFeldModus VereinFeld { get; set; } = FormularFeldModus.Optional;

        public FormularFeldModus GraduierungFeld { get; set; } = FormularFeldModus.Optional;

        public FormularFeldModus BemerkungFeld { get; set; } = FormularFeldModus.Optional;

        /// <summary>Uhrzeit (Ortszeit) der täglichen Zusammenfassung für Organisator-Benachrichtigungen.</summary>
        public TimeOnly ZusammenfassungUhrzeit { get; set; } = new(7, 0);

        [MaxLength(450)]
        public string ErstelltVon { get; set; } = string.Empty;

        public DateTime ErstelltUtc { get; set; }

        [MaxLength(450)]
        public string? GeaendertVon { get; set; }

        public DateTime? GeaendertUtc { get; set; }

        /// <summary>Erkennt gleichzeitige Bearbeitung durch zwei Organisatoren.</summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public ICollection<VeranstaltungsTag> Tage { get; set; } = new List<VeranstaltungsTag>();

        public ICollection<Anmeldung> Anmeldungen { get; set; } = new List<Anmeldung>();

        public ICollection<BenachrichtigungEmpfaenger> BenachrichtigungEmpfaenger { get; set; } = new List<BenachrichtigungEmpfaenger>();
    }
}
