using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>
    /// Empfänger von Organisator-Benachrichtigungen (An-/Abmeldungen, Änderungen) einer Veranstaltung.
    /// Entweder ein Website-Benutzer (UserId, Adresse wird beim Versand aus dem Konto gelesen)
    /// oder eine freie E-Mail-Adresse – genau eines von beiden (Check-Constraint).
    /// </summary>
    public class BenachrichtigungEmpfaenger
    {
        [Key]
        public int Id { get; set; }

        public int VeranstaltungId { get; set; }

        public Veranstaltung? Veranstaltung { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        public ApplicationUser? User { get; set; }

        [MaxLength(320)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? Name { get; set; }

        public BenachrichtigungEreignisse Ereignisse { get; set; } = BenachrichtigungEreignisse.Alle;

        public BenachrichtigungModus Modus { get; set; } = BenachrichtigungModus.Sofort;

        /// <summary>Nur bei freien Adressen: SHA-256 des Abmeldetokens.</summary>
        [MaxLength(32)]
        public byte[]? AbmeldeTokenHash { get; set; }

        public DateTime? AbgemeldetUtc { get; set; }

        /// <summary>Ereignisse bis zu diesem Zeitpunkt sind diesem Empfänger bereits gemeldet.</summary>
        public DateTime BenachrichtigtBisUtc { get; set; }

        [MaxLength(450)]
        public string HinzugefuegtVon { get; set; } = string.Empty;

        public DateTime ErstelltUtc { get; set; }
    }
}
