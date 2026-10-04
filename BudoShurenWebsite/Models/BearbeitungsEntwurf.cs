using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    /// <summary>
    /// Nicht gespeicherter Stand einer Bearbeitungsseite (siehe Services/Entwuerfe): je Benutzer und Seite höchstens einer.
    /// Damit übersteht die Bearbeitung ein Neuladen der Seite, z. B. nach einem Tab-Wechsel auf dem Handy.
    /// </summary>
    public class BearbeitungsEntwurf
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string BenutzerId { get; set; } = string.Empty;

        /// <summary>Welche Seite und welcher Datensatz, z. B. "veranstaltung:12" oder "veranstaltung:neu".</summary>
        [MaxLength(100)]
        public string Schluessel { get; set; } = string.Empty;

        /// <summary>Formularmodell der Seite als JSON.</summary>
        public string Daten { get; set; } = string.Empty;

        public DateTime GeaendertUtc { get; set; }
    }
}
