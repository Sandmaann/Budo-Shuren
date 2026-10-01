using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>Protokoll einer Nachricht der Organisatoren an die Teilnehmer (Rundmail, Absage eines Tages oder der Veranstaltung).</summary>
    public class VeranstaltungNachricht
    {
        [Key]
        public int Id { get; set; }

        public int VeranstaltungId { get; set; }

        public Veranstaltung? Veranstaltung { get; set; }

        public NachrichtArt Art { get; set; }

        [Required]
        [MaxLength(200)]
        public string Betreff { get; set; } = string.Empty;

        /// <summary>Markdown, wie vom Organisator eingegeben.</summary>
        public string InhaltMarkdown { get; set; } = string.Empty;

        /// <summary>Ob auch die Info-Adressen (Begleitpersonen) die Nachricht bekommen haben.</summary>
        public bool AnInfoAdressen { get; set; }

        public int AnzahlEmpfaenger { get; set; }

        [MaxLength(450)]
        public string ErstelltVon { get; set; } = string.Empty;

        public DateTime GesendetUtc { get; set; }
    }
}
