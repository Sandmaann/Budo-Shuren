using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum AktuellesBlockTyp
    {
        [Description("Markdown-Textblock")]
        MarkdownText = 0,

        [Description("Bilder-Galerie mit konfigurierbarer Anzahl pro Reihe")]
        BilderGalerie = 1
    }
}
