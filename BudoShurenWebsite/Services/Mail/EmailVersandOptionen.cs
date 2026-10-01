namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>Einstellungen für den Mailversand aus der Warteschlange (appsettings-Abschnitt "EmailVersand", alles optional).</summary>
    public sealed class EmailVersandOptionen
    {
        public const string Abschnitt = "EmailVersand";

        /// <summary>false schaltet den Hintergrundversand ab (z. B. in Tests). Eingereiht wird trotzdem.</summary>
        public bool Aktiviert { get; set; } = true;

        /// <summary>Wie oft ohne Anstoß nach fälligen Mails gesucht wird.</summary>
        public TimeSpan Abfrageintervall { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>Höchstzahl Mails pro SMTP-Verbindung.</summary>
        public int StapelGroesse { get; set; } = 50;

        /// <summary>Pause zwischen zwei Mails, um Limits des SMTP-Anbieters nicht zu überschreiten.</summary>
        public TimeSpan PauseZwischenMails { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>Nach so vielen Fehlversuchen gilt eine Mail als endgültig fehlgeschlagen.</summary>
        public int MaxVersuche { get; set; } = 5;

        /// <summary>Wartezeit, wenn der Versand als Ganzes scheitert (z. B. SMTP-Server nicht erreichbar).</summary>
        public TimeSpan WartezeitNachFehler { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>Versendete und fehlgeschlagene Einträge werden danach gelöscht (enthalten personenbezogene Daten).</summary>
        public int AufbewahrungTage { get; set; } = 30;

        /// <summary>Wird beim App-Start geprüft. Eine StapelGroesse von 0 würde den Versand z. B. in eine Endlosschleife schicken.</summary>
        public bool IstGueltig() =>
            StapelGroesse > 0
            && MaxVersuche > 0
            && AufbewahrungTage > 0
            && Abfrageintervall > TimeSpan.Zero
            && WartezeitNachFehler > TimeSpan.Zero
            && PauseZwischenMails >= TimeSpan.Zero;
    }
}
