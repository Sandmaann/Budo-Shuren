using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>
    /// Anmeldung einer Person (ohne Benutzerkonto) zu einer Veranstaltung. Pro Veranstaltung und E-Mail-Adresse
    /// gibt es genau einen Datensatz; nach einer Abmeldung wird er bei erneuter Anmeldung wiederverwendet.
    /// </summary>
    public class Anmeldung
    {
        [Key]
        public int Id { get; set; }

        public int VeranstaltungId { get; set; }

        public Veranstaltung? Veranstaltung { get; set; }

        /// <summary>Kleingeschrieben und ohne Leerzeichen gespeichert.</summary>
        [Required]
        [MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Vorname { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nachname { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Telefon { get; set; }

        [MaxLength(200)]
        public string? Verein { get; set; }

        [MaxLength(100)]
        public string? Graduierung { get; set; }

        [MaxLength(2000)]
        public string? Bemerkung { get; set; }

        public int AnzahlBegleitpersonen { get; set; }

        public AnmeldungStatus Status { get; set; } = AnmeldungStatus.Unbestaetigt;

        /// <summary>Z. B. der Ablehnungsgrund.</summary>
        [MaxLength(1000)]
        public string? StatusGrund { get; set; }

        public AnmeldungQuelle Quelle { get; set; } = AnmeldungQuelle.Formular;

        /// <summary>SHA-256 des Verwaltungstokens. Der Klartext steht nur im Link der Mail.</summary>
        [MaxLength(32)]
        public byte[] TokenHash { get; set; } = [];

        public DateTime TokenErstelltUtc { get; set; }

        /// <summary>Neue Adresse, die der Teilnehmer noch bestätigen muss.</summary>
        [MaxLength(320)]
        public string? NeueEmail { get; set; }

        [MaxLength(32)]
        public byte[]? NeueEmailTokenHash { get; set; }

        /// <summary>Solange gesetzt und nicht abgelaufen, belegt eine unbestätigte Anmeldung ihre Plätze.</summary>
        public DateTime? ReserviertBisUtc { get; set; }

        public DateTime? EmailBestaetigtUtc { get; set; }

        public DateTime DatenschutzAkzeptiertUtc { get; set; }

        /// <summary>Interne Notiz des Organisators, für Teilnehmer nicht sichtbar.</summary>
        [MaxLength(2000)]
        public string? AdminNotiz { get; set; }

        /// <summary>Ereignisse nach diesem Zeitpunkt gelten in der Übersicht als "geändert".</summary>
        public DateTime? AdminGesehenUtc { get; set; }

        public DateTime ErstelltUtc { get; set; }

        public DateTime? GeaendertUtc { get; set; }

        /// <summary>Erkennt gleichzeitige Änderungen durch Teilnehmer und Organisator.</summary>
        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        /// <summary>Nur bei Teilnahmemodus EinzelneTage gefüllt; sonst gilt die Anmeldung für alle Tage.</summary>
        public ICollection<AnmeldungTag> Tage { get; set; } = new List<AnmeldungTag>();

        public ICollection<AnmeldungInfoEmail> InfoEmails { get; set; } = new List<AnmeldungInfoEmail>();

        public ICollection<AnmeldungEreignis> Ereignisse { get; set; } = new List<AnmeldungEreignis>();
    }
}
