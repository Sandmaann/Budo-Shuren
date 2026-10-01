using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Erlaubte Statuswechsel einer Anmeldung und wer sie auslösen darf.
    /// Das Verwerfen abgelaufener unbestätigter Anmeldungen ist kein Statuswechsel (der Datensatz wird gelöscht),
    /// außer die Anmeldung war früher schon bestätigt: dann setzt VeranstaltungWartungJob sie auf Storniert.
    /// </summary>
    public static class AnmeldungStatusUebergaenge
    {
        private static readonly EreignisAkteur[] Alle = [EreignisAkteur.Teilnehmer, EreignisAkteur.Admin, EreignisAkteur.System];
        private static readonly EreignisAkteur[] NurOrganisation = [EreignisAkteur.Admin, EreignisAkteur.System];
        private static readonly EreignisAkteur[] NurAdmin = [EreignisAkteur.Admin];

        private static readonly Dictionary<(AnmeldungStatus Von, AnmeldungStatus Nach), EreignisAkteur[]> Erlaubt = new()
        {
            // E-Mail bestätigt (Teilnehmer) bzw. ohne Double-Opt-In / manuell angelegt
            [(AnmeldungStatus.Unbestaetigt, AnmeldungStatus.Angemeldet)] = Alle,
            [(AnmeldungStatus.Unbestaetigt, AnmeldungStatus.Warteliste)] = Alle,
            [(AnmeldungStatus.Unbestaetigt, AnmeldungStatus.Storniert)] = Alle,
            [(AnmeldungStatus.Unbestaetigt, AnmeldungStatus.Abgelehnt)] = NurAdmin,

            [(AnmeldungStatus.Angemeldet, AnmeldungStatus.Storniert)] = Alle,
            [(AnmeldungStatus.Angemeldet, AnmeldungStatus.Abgelehnt)] = NurAdmin,

            // Nachrücken (Phase 2)
            [(AnmeldungStatus.Warteliste, AnmeldungStatus.Angemeldet)] = NurOrganisation,
            [(AnmeldungStatus.Warteliste, AnmeldungStatus.Storniert)] = Alle,
            [(AnmeldungStatus.Warteliste, AnmeldungStatus.Abgelehnt)] = NurAdmin,

            // Erneute Anmeldung nach Abmeldung: über das Formular mit Opt-In, durch den Admin direkt
            [(AnmeldungStatus.Storniert, AnmeldungStatus.Unbestaetigt)] = [EreignisAkteur.Teilnehmer],
            [(AnmeldungStatus.Storniert, AnmeldungStatus.Angemeldet)] = Alle,

            // Ablehnung zurücknehmen; Teilnehmer können eine Ablehnung nie selbst aufheben
            [(AnmeldungStatus.Abgelehnt, AnmeldungStatus.Angemeldet)] = NurAdmin,
        };

        public static bool IstErlaubt(AnmeldungStatus von, AnmeldungStatus nach, EreignisAkteur akteur) =>
            Erlaubt.TryGetValue((von, nach), out var akteure) && akteure.Contains(akteur);

        /// <exception cref="InvalidOperationException">Wenn der Wechsel nicht erlaubt ist.</exception>
        public static void Pruefen(AnmeldungStatus von, AnmeldungStatus nach, EreignisAkteur akteur)
        {
            if (!IstErlaubt(von, nach, akteur))
                throw new InvalidOperationException($"Statuswechsel von {von} nach {nach} ist für {akteur} nicht erlaubt.");
        }

        /// <summary>Aktive Anmeldungen zählen für Teilnehmerlisten und verhindern z. B. das Ändern des Teilnahmemodus.</summary>
        public static bool IstAktiv(AnmeldungStatus status) =>
            status is AnmeldungStatus.Unbestaetigt or AnmeldungStatus.Angemeldet or AnmeldungStatus.Warteliste;
    }
}
