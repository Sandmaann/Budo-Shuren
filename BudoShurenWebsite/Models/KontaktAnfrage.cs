using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    /// <summary>
    /// Protokoll des Kontaktformulars (InteresseFormular): jede abgeschickte Nachricht, auch die als Spam abgelehnten.
    /// Geschrieben von KontaktProtokoll. Absender und Nachricht werden nach KontaktProtokoll.AufbewahrenFuer geleert,
    /// die Zeile bleibt für die Auswertung im Adminbereich.
    /// </summary>
    public class KontaktAnfrage
    {
        public const int MaxNachricht = 4000;

        [Key]
        public int Id { get; set; }

        public DateTime ZeitpunktUtc { get; set; }

        /// <summary>Name der Abteilung, an die die Nachricht ging (kein Fremdschlüssel: bleibt, wenn die Abteilung gelöscht wird).</summary>
        [MaxLength(200)]
        public string? Abteilung { get; set; }

        [MaxLength(320)]
        public string? AbsenderEmail { get; set; }

        /// <summary>Text der Nachricht, höchstens <see cref="MaxNachricht"/> Zeichen.</summary>
        [MaxLength(MaxNachricht)]
        public string? Nachricht { get; set; }

        public KontaktAnfrageStatus Status { get; set; }

        /// <summary>Einzelheit zum Status, z. B. "3 Links".</summary>
        [MaxLength(500)]
        public string? Hinweis { get; set; }

        /// <summary>Anzahl der Mails, die an die Abteilung und ihre Abteilungsleiter gingen.</summary>
        public int AnzahlMails { get; set; }

        /// <summary>Seite, auf der das Formular stand, z. B. "Kontakt".</summary>
        [MaxLength(200)]
        public string? Seite { get; set; }

        /// <summary>
        /// true: über die laufende Blazor-Verbindung abgeschickt (Browser mit JavaScript).
        /// false: als gewöhnlicher Formular-POST, wie ihn auch ein einfaches Skript schicken kann.
        /// </summary>
        public bool Interaktiv { get; set; }
    }
}
