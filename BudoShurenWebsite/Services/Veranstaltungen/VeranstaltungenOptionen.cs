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
    }
}
