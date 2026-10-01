using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>
    /// Zusätzliche Adresse, die Infos zur Anmeldung bekommt (z. B. für Begleitpersonen).
    /// Daten Dritter: jede Mail enthält einen eigenen Abmeldelink, es gibt keinen Verwaltungslink.
    /// </summary>
    public class AnmeldungInfoEmail
    {
        [Key]
        public int Id { get; set; }

        public int AnmeldungId { get; set; }

        public Anmeldung? Anmeldung { get; set; }

        [Required]
        [MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        /// <summary>SHA-256 des Abmeldetokens.</summary>
        [MaxLength(32)]
        public byte[] AbmeldeTokenHash { get; set; } = [];

        public DateTime? AbgemeldetUtc { get; set; }
    }
}
