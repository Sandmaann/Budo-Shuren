using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Ein Ereignis einer Anmeldung, wie es die Auswertung braucht.</summary>
    public sealed record BenachrichtigungEreignis(
        int AnmeldungId,
        DateTime ZeitpunktUtc,
        AnmeldungEreignisArt Art,
        EreignisAkteur Akteur,
        string? AkteurUserId,
        string? DetailsJson);

    /// <summary>Stand eines Empfängers. UserId nur bei Website-Benutzern (dessen eigene Aktionen werden ihm nicht gemeldet).</summary>
    public sealed record EmpfaengerStand(
        BenachrichtigungModus Modus,
        BenachrichtigungEreignisse Ereignisse,
        string? UserId,
        bool Abgemeldet,
        DateTime BenachrichtigtBisUtc);

    /// <param name="AnmeldeschlussUtc">Nur ein ausdrücklich gesetzter Anmeldeschluss (AnmeldungBis), sonst null.</param>
    /// <param name="Ausgebucht">Jetzt kann sich niemand mehr anmelden (KapazitaetsRechner).</param>
    /// <param name="UnbestaetigteAnmeldungen">Änderungen an diesen Anmeldungen interessieren (noch) nicht.</param>
    public sealed record VeranstaltungStand(
        bool DoubleOptIn,
        DateTime? AnmeldeschlussUtc,
        TimeOnly ZusammenfassungUhrzeit,
        bool Ausgebucht,
        IReadOnlySet<int> UnbestaetigteAnmeldungen);

    /// <summary>Was einem Empfänger gemeldet wird; NeuBisUtc wird danach als BenachrichtigtBisUtc gespeichert.</summary>
    public sealed record BenachrichtigungsPlan(
        DateTime NeuBisUtc,
        IReadOnlyList<BenachrichtigungEreignis> Meldungen,
        bool Ausgebucht,
        bool Anmeldeschluss)
    {
        public bool Senden => Meldungen.Count > 0 || Ausgebucht || Anmeldeschluss;
    }

    /// <summary>
    /// Entscheidet für einen Empfänger, ob und worüber er jetzt benachrichtigt wird (Plan 2.9). Ohne Datenbank, damit testbar.
    /// <list type="bullet">
    /// <item>Betrachtet werden Ereignisse nach BenachrichtigtBisUtc bis "jetzt minus Puffer": Ein Ereignis, dessen Transaktion
    /// noch nicht gespeichert ist, kann so nicht übersprungen werden.</item>
    /// <item>Sofort: erst nach <see cref="Ruhe"/> ohne neue Ereignisse, damit mehrere Änderungen eine Mail ergeben;
    /// bei Dauerbetrieb spätestens nach <see cref="MaxVerzoegerung"/>.</item>
    /// <item>Tägliche Zusammenfassung: einmal, sobald die Uhrzeit (Ortszeit) vorbei ist.</item>
    /// <item>Pausiert oder abgemeldet: nichts senden, Stand trotzdem fortschreiben (beim Wiedereinschalten keine Flut).</item>
    /// </list>
    /// </summary>
    public static class BenachrichtigungsAuswertung
    {
        public static readonly TimeSpan Puffer = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan Ruhe = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan MaxVerzoegerung = TimeSpan.FromMinutes(30);

        /// <param name="ereignisse">Alle Ereignisse der Veranstaltung nach empfaenger.BenachrichtigtBisUtc (auch die jüngsten).</param>
        /// <returns>null, wenn noch nicht fällig (nichts speichern).</returns>
        public static BenachrichtigungsPlan? Auswerten(
            EmpfaengerStand empfaenger,
            VeranstaltungStand veranstaltung,
            IReadOnlyCollection<BenachrichtigungEreignis> ereignisse,
            DateTime jetztUtc)
        {
            var bis = empfaenger.BenachrichtigtBisUtc;
            var grenze = jetztUtc - Puffer;
            var neuBis = grenze > bis ? grenze : bis;
            var offen = ereignisse.Where(e => e.ZeitpunktUtc > bis).ToList();

            if (empfaenger.Abgemeldet || empfaenger.Modus == BenachrichtigungModus.Pausiert)
                return new BenachrichtigungsPlan(neuBis, [], false, false);

            if (empfaenger.Modus == BenachrichtigungModus.TaeglicheZusammenfassung)
            {
                var termin = LetzterTerminUtc(veranstaltung.ZusammenfassungUhrzeit, grenze);
                if (termin <= bis)
                    return null;
            }
            else if (offen.Count > 0)
            {
                var neuestes = offen.Max(e => e.ZeitpunktUtc);
                var aeltestes = offen.Min(e => e.ZeitpunktUtc);
                if (jetztUtc - neuestes < Ruhe && jetztUtc - aeltestes < MaxVerzoegerung)
                    return null;
            }

            var imFenster = offen.Where(e => e.ZeitpunktUtc <= grenze).OrderBy(e => e.ZeitpunktUtc).ToList();

            var meldungen = imFenster
                .Where(e => e.AkteurUserId is null || e.AkteurUserId != empfaenger.UserId)
                .Where(e => Kategorie(e, veranstaltung) is { } kategorie && empfaenger.Ereignisse.HasFlag(kategorie))
                .ToList();

            var ausgebucht = empfaenger.Ereignisse.HasFlag(BenachrichtigungEreignisse.Ausgebucht)
                && veranstaltung.Ausgebucht
                && imFenster.Any(e => BelegtPlaetze(e, veranstaltung));

            var anmeldeschluss = empfaenger.Ereignisse.HasFlag(BenachrichtigungEreignisse.AnmeldeschlussErreicht)
                && veranstaltung.AnmeldeschlussUtc is { } schluss
                && schluss > bis && schluss <= grenze;

            return new BenachrichtigungsPlan(neuBis, meldungen, ausgebucht, anmeldeschluss);
        }

        /// <summary>
        /// Wozu ein Ereignis gehört; null = wird nie gemeldet (z. B. unbestätigte Anmeldung, Aktionen des Systems, Link versendet).
        /// </summary>
        public static BenachrichtigungEreignisse? Kategorie(BenachrichtigungEreignis e, VeranstaltungStand v)
        {
            if (e.Akteur == EreignisAkteur.System)
                return null;

            return e.Art switch
            {
                AnmeldungEreignisArt.Bestaetigt or AnmeldungEreignisArt.AblehnungZurueckgenommen => BenachrichtigungEreignisse.NeueAnmeldung,
                // Mit Double-Opt-In zählt eine Anmeldung erst mit der Bestätigung; manuell angelegte sind sofort gültig
                AnmeldungEreignisArt.Angelegt or AnmeldungEreignisArt.Reaktiviert =>
                    e.Akteur == EreignisAkteur.Admin || !v.DoubleOptIn ? BenachrichtigungEreignisse.NeueAnmeldung : null,
                AnmeldungEreignisArt.Storniert or AnmeldungEreignisArt.Abgelehnt => BenachrichtigungEreignisse.Abmeldung,
                AnmeldungEreignisArt.DatenGeaendert or AnmeldungEreignisArt.TageGeaendert or AnmeldungEreignisArt.BegleitungGeaendert
                    or AnmeldungEreignisArt.InfoEmailsGeaendert or AnmeldungEreignisArt.EmailGeaendert or AnmeldungEreignisArt.AdminBearbeitet =>
                    v.UnbestaetigteAnmeldungen.Contains(e.AnmeldungId) ? null : BenachrichtigungEreignisse.Aenderung,
                _ => null
            };
        }

        /// <summary>Letzter Zeitpunkt der täglichen Zusammenfassung (Ortszeit) bis einschließlich zeitpunktUtc, in UTC.</summary>
        public static DateTime LetzterTerminUtc(TimeOnly uhrzeit, DateTime zeitpunktUtc)
        {
            var ortszeit = Ortszeit.AusUtc(zeitpunktUtc);
            var heute = Ortszeit.NachUtc(DateOnly.FromDateTime(ortszeit).ToDateTime(uhrzeit));
            return heute <= zeitpunktUtc
                ? heute
                : Ortszeit.NachUtc(DateOnly.FromDateTime(ortszeit).AddDays(-1).ToDateTime(uhrzeit));
        }

        // Ereignisse, nach denen die Veranstaltung voll sein kann
        private static bool BelegtPlaetze(BenachrichtigungEreignis e, VeranstaltungStand v) =>
            e.Art is AnmeldungEreignisArt.TageGeaendert or AnmeldungEreignisArt.BegleitungGeaendert or AnmeldungEreignisArt.AdminBearbeitet
            || Kategorie(e, v) == BenachrichtigungEreignisse.NeueAnmeldung;
    }
}
