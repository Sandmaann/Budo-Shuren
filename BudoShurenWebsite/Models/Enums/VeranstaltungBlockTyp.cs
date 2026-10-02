using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    /// <summary>Bausteine der Beschreibung einer Veranstaltung (wie bei Aktuelles).</summary>
    public enum VeranstaltungBlockTyp
    {
        [Description("Text")]
        MarkdownText = 0,

        [Description("Bilder")]
        BilderGalerie = 1
    }
}
