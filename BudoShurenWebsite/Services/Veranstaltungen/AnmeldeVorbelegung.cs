using BudoShurenWebsite.Data;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Füllt das Anmeldeformular für eingeloggte Website-Benutzer mit Vorname, Nachname und E-Mail aus ihrem Konto vor.
    /// Die Anmeldung selbst läuft danach wie bei Gästen (inkl. Double-Opt-In); die Werte bleiben änderbar.
    /// </summary>
    public static class AnmeldeVorbelegung
    {
        /// <summary>Setzt nur Felder, die noch leer sind, und nur mit Werten, die im Konto gepflegt sind.</summary>
        public static void Uebernehmen(AnmeldeEingabe eingabe, ApplicationUser benutzer)
        {
            eingabe.Vorname = WennLeer(eingabe.Vorname, benutzer.Vorname);
            eingabe.Nachname = WennLeer(eingabe.Nachname, benutzer.Name);
            eingabe.Email = WennLeer(eingabe.Email, benutzer.Email);
        }

        private static string? WennLeer(string? aktuell, string? ausKonto) =>
            string.IsNullOrWhiteSpace(aktuell) && !string.IsNullOrWhiteSpace(ausKonto) ? ausKonto.Trim() : aktuell;
    }
}
