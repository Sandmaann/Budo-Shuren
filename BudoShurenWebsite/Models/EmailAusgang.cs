using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    /// <summary>
    /// Eine Mail in der Versand-Warteschlange. Wird zusammen mit den fachlichen Daten gespeichert
    /// und danach vom EmailVersandHostedService verschickt (siehe Services/Mail).
    /// Alle Zeitpunkte in UTC.
    /// </summary>
    public class EmailAusgang
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(320)]
        public string An { get; set; } = string.Empty;

        /// <summary>Reply-To, z. B. die Kontaktadresse einer Veranstaltung. Absender ist immer die System-Adresse.</summary>
        [MaxLength(320)]
        public string? AntwortAn { get; set; }

        [Required]
        [MaxLength(500)]
        public string Betreff { get; set; } = string.Empty;

        [Required]
        public string Html { get; set; } = string.Empty;

        /// <summary>Anhänge als JSON-Liste von EmailAnhang (Inhalt Base64).</summary>
        public string? AnhaengeJson { get; set; }

        public EmailPrioritaet Prioritaet { get; set; } = EmailPrioritaet.Normal;

        public EmailStatus Status { get; set; } = EmailStatus.Wartend;

        /// <summary>Anzahl fehlgeschlagener Versandversuche.</summary>
        public int Versuche { get; set; }

        [MaxLength(2000)]
        public string? LetzterFehler { get; set; }

        public DateTime ErstelltUtc { get; set; }

        /// <summary>Frühester Versandzeitpunkt; wird nach einem Fehlschlag nach hinten verschoben.</summary>
        public DateTime FaelligAbUtc { get; set; }

        public DateTime? GesendetAmUtc { get; set; }

        /// <summary>Fachlicher Bezug, z. B. "Anmeldung". Für Versandprotokoll und Datenlöschung.</summary>
        [MaxLength(100)]
        public string? BezugTyp { get; set; }

        public int? BezugId { get; set; }
    }
}
