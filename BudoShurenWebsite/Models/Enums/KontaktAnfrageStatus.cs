namespace BudoShurenWebsite.Models.Enums
{
    /// <summary>
    /// Was aus einer Nachricht des Kontaktformulars wurde. Die Zahlen stehen in der Datenbank und dürfen sich nicht ändern.
    /// Werte ab <see cref="Honeypot"/> sind Ablehnungen: dann ging keine Mail raus.
    /// </summary>
    public enum KontaktAnfrageStatus
    {
        /// <summary>Mindestens eine Mail an die Abteilung ging raus.</summary>
        Zugestellt = 0,

        /// <summary>Angenommen, aber an keine Adresse der Abteilung ließ sich eine Mail schicken.</summary>
        VersandFehler = 1,

        /// <summary>Das versteckte Feld war ausgefüllt.</summary>
        Honeypot = 10,

        /// <summary>Schneller abgeschickt, als ein Mensch das Formular ausfüllt.</summary>
        ZuSchnell = 11,

        /// <summary>Zeitstempel des Formulars fehlt, ist manipuliert oder abgelaufen.</summary>
        FormularUngueltig = 12,

        /// <summary>Die Nachricht sieht nach Werbung aus (mehrere Links, HTML).</summary>
        Inhalt = 13,

        /// <summary>Zu viele Nachrichten in kurzer Zeit (je Absender oder insgesamt).</summary>
        ZuVieleAnfragen = 14,
    }

    public static class KontaktAnfrageStatusTexte
    {
        public static bool IstAbgelehnt(this KontaktAnfrageStatus status) => status >= KontaktAnfrageStatus.Honeypot;

        /// <summary>Bezeichnung für die Auswertung im Adminbereich.</summary>
        public static string Bezeichnung(this KontaktAnfrageStatus status) => status switch
        {
            KontaktAnfrageStatus.Zugestellt => "Zugestellt",
            KontaktAnfrageStatus.VersandFehler => "Versandfehler",
            KontaktAnfrageStatus.Honeypot => "Abgelehnt: verstecktes Feld ausgefüllt",
            KontaktAnfrageStatus.ZuSchnell => "Abgelehnt: zu schnell abgeschickt",
            KontaktAnfrageStatus.FormularUngueltig => "Abgelehnt: Formular ungültig oder abgelaufen",
            KontaktAnfrageStatus.Inhalt => "Abgelehnt: Inhalt",
            KontaktAnfrageStatus.ZuVieleAnfragen => "Abgelehnt: zu viele Nachrichten",
            _ => status.ToString()
        };
    }
}
