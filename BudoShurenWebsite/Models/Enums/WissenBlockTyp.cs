using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum WissenBlockTyp
    {
        // Bestehende Typen (behalten für DB-Kompatibilität, im Editor nicht mehr auswählbar)
        [Description("Einfacher Textabsatz ohne Layout-Struktur")]
        TextAbsatz = 0,
        [Description("Horizontale Trennlinie mit Abschnittsüberschrift")]
        SectionBanner = 1,
        [Description("Große Zwischenüberschrift im Yuji-Stil")]
        Subheadline = 2,
        [Description("Bild links oder rechts, Text daneben")]
        BildMitText = 3,
        [Description("Bild zentriert, ohne Text")]
        BildEinzel = 4,
        [Description("Stichpunktliste")]
        Aufzaehlung = 5,

        // Neue Typen nach konkreten Design-Vorlagen
        [Description("Fließtext links, Bild schwebt rechts daneben (max. 288 px)")]
        TextUndBildRechts = 10,

        [Description("Große Überschrift links, eingerückter Text rechts daneben")]
        UeberschriftMitEingeruecktemText = 11,

        [Description("Große Überschrift links, erster Text rechts, zweiter Text + opt. Bild darunter")]
        UeberschriftMitZweiTextbloecken = 12,

        [Description("Fließtext links, Überschrift im Yuji-Stil rechts daneben")]
        TextMitUeberschriftRechts = 13,

        [Description("Fette Zwischenüberschrift, darunter Bild rechts schwebend mit Fließtext")]
        ZwischenüberschriftUndBildMitText = 14,
    }
}
