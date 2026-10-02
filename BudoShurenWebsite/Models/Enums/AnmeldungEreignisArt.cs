using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum AnmeldungEreignisArt
    {
        [Description("Angemeldet (unbestätigt)")]
        Angelegt = 0,

        [Description("E-Mail bestätigt")]
        Bestaetigt = 1,

        [Description("Daten geändert")]
        DatenGeaendert = 2,

        [Description("Termine geändert")]
        TageGeaendert = 3,

        [Description("Begleitpersonen geändert")]
        BegleitungGeaendert = 4,

        [Description("Info-Adressen geändert")]
        InfoEmailsGeaendert = 5,

        [Description("E-Mail-Adresse geändert")]
        EmailGeaendert = 6,

        [Description("Abgemeldet")]
        Storniert = 7,

        [Description("Erneut angemeldet")]
        Reaktiviert = 8,

        [Description("Abgelehnt")]
        Abgelehnt = 9,

        [Description("Ablehnung zurückgenommen")]
        AblehnungZurueckgenommen = 10,

        [Description("Vom Organisator bearbeitet")]
        AdminBearbeitet = 11,

        [Description("Verwaltungslink versendet")]
        LinkVersendet = 12,

        [Description("Termin abgesagt")]
        TagAbgesagt = 13
    }
}
