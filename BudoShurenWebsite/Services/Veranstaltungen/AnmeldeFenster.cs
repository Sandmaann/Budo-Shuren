using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    public enum AnmeldeZustand
    {
        Offen,
        NochNichtOffen,
        Geschlossen
    }

    /// <summary>Ob man sich gerade anmelden kann (Ortszeit).</summary>
    public static class AnmeldeFenster
    {
        /// <summary>Beginn des ersten nicht abgesagten Tages; null ohne aktive Tage.</summary>
        public static DateTime? Beginn(IReadOnlyCollection<VeranstaltungsTag> tage) =>
            tage.Where(t => !t.Abgesagt).Select(t => (DateTime?)t.Datum.ToDateTime(t.Beginn)).Min();

        /// <summary>
        /// Offen, wenn veröffentlicht, nach AnmeldungAb und vor AnmeldungBis.
        /// Ohne AnmeldungBis endet die Anmeldung mit dem Beginn der Veranstaltung.
        /// </summary>
        public static AnmeldeZustand Zustand(Veranstaltung veranstaltung, IReadOnlyCollection<VeranstaltungsTag> tage, DateTime jetztOrtszeit)
        {
            var beginn = Beginn(tage);
            if (veranstaltung.Status != VeranstaltungStatus.Veroeffentlicht || beginn is null)
                return AnmeldeZustand.Geschlossen;
            if (veranstaltung.AnmeldungAb is { } ab && jetztOrtszeit < ab)
                return AnmeldeZustand.NochNichtOffen;

            var schluss = veranstaltung.AnmeldungBis ?? beginn.Value;
            return jetztOrtszeit < schluss ? AnmeldeZustand.Offen : AnmeldeZustand.Geschlossen;
        }

        /// <summary>Teilnehmer dürfen Daten, Tage und Begleitpersonen ändern: bis zur Änderungsfrist, sonst bis zum Beginn.</summary>
        public static bool AenderungenMoeglich(Veranstaltung veranstaltung, IReadOnlyCollection<VeranstaltungsTag> tage, DateTime jetztOrtszeit) =>
            veranstaltung.Status == VeranstaltungStatus.Veroeffentlicht
            && Beginn(tage) is { } beginn
            && jetztOrtszeit < (veranstaltung.AenderungenBis ?? beginn);

        /// <summary>Abmelden geht immer bis zum Beginn, auch nach der Änderungsfrist.</summary>
        public static bool AbmeldenMoeglich(Veranstaltung veranstaltung, IReadOnlyCollection<VeranstaltungsTag> tage, DateTime jetztOrtszeit) =>
            veranstaltung.Status == VeranstaltungStatus.Veroeffentlicht
            && Beginn(tage) is { } beginn
            && jetztOrtszeit < beginn;
    }
}
