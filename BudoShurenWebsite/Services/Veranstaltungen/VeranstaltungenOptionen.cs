namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Einstellungen des Moduls (appsettings-Abschnitt "Veranstaltungen").</summary>
    public sealed class VeranstaltungenOptionen
    {
        public const string Abschnitt = "Veranstaltungen";

        /// <summary>
        /// Feature-Schalter: solange false, sind Menüeintrag und Seiten des Moduls ausgeblendet.
        /// Die Datenbanktabellen existieren trotzdem (Migrationen laufen immer).
        /// </summary>
        public bool Aktiviert { get; set; }

        /// <summary>So lange hält eine unbestätigte Anmeldung ihre Plätze frei (Double-Opt-In).</summary>
        public int ReservierungStunden { get; set; } = 24;

        /// <summary>Höchstzahl Info-Adressen je Anmeldung (zusätzlich höchstens so viele wie Begleitpersonen).</summary>
        public int MaxInfoEmails { get; set; } = 5;
    }
}
