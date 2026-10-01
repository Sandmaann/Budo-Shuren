using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Kapazität eines Veranstaltungstags. MaxTeilnehmer null = unbegrenzt.</summary>
    public sealed record TagKapazitaet(int TagId, int? MaxTeilnehmer, bool Abgesagt);

    /// <summary>Für die Platzberechnung relevante Daten einer Anmeldung.</summary>
    /// <param name="TagIds">Gebuchte Tage; nur bei Teilnahmemodus EinzelneTage relevant.</param>
    public sealed record AnmeldungBelegung(
        int AnmeldungId,
        AnmeldungStatus Status,
        DateTime? ReserviertBisUtc,
        int AnzahlBegleitpersonen,
        IReadOnlyCollection<int> TagIds);

    public sealed record KapazitaetsPruefung(bool Passt, IReadOnlyList<int> VolleTagIds);

    /// <summary>
    /// Berechnet belegte und freie Plätze pro Tag. Gezählt werden Personen (Anmelder + Begleitpersonen).
    /// Plätze belegen: bestätigte Anmeldungen und unbestätigte, solange ihre Reservierung läuft.
    /// Warteliste, Abmeldungen und Ablehnungen belegen nichts.
    /// </summary>
    public static class KapazitaetsRechner
    {
        public static bool BelegtPlaetze(AnmeldungStatus status, DateTime? reserviertBisUtc, DateTime jetztUtc) =>
            status switch
            {
                AnmeldungStatus.Angemeldet => true,
                AnmeldungStatus.Unbestaetigt => reserviertBisUtc > jetztUtc,
                _ => false
            };

        /// <summary>Tage, für die eine Anmeldung gilt: bei NurGesamt alle nicht abgesagten Tage, sonst die gebuchten.</summary>
        public static IReadOnlyCollection<int> GueltigeTage(
            Teilnahmemodus modus, IReadOnlyCollection<TagKapazitaet> tage, IReadOnlyCollection<int> gebuchteTagIds)
        {
            var aktiveTage = tage.Where(t => !t.Abgesagt).Select(t => t.TagId);
            return modus == Teilnahmemodus.NurGesamt
                ? aktiveTage.ToList()
                : aktiveTage.Intersect(gebuchteTagIds).ToList();
        }

        /// <summary>Belegte Plätze je nicht abgesagtem Tag.</summary>
        /// <param name="ohneAnmeldungId">Diese Anmeldung nicht mitzählen (z. B. beim Umbuchen der eigenen Anmeldung).</param>
        public static IReadOnlyDictionary<int, int> BelegungProTag(
            Teilnahmemodus modus,
            IReadOnlyCollection<TagKapazitaet> tage,
            IEnumerable<AnmeldungBelegung> anmeldungen,
            DateTime jetztUtc,
            int? ohneAnmeldungId = null)
        {
            var belegung = tage.Where(t => !t.Abgesagt).ToDictionary(t => t.TagId, _ => 0);

            foreach (var anmeldung in anmeldungen)
            {
                if (anmeldung.AnmeldungId == ohneAnmeldungId || !BelegtPlaetze(anmeldung.Status, anmeldung.ReserviertBisUtc, jetztUtc))
                    continue;

                var personen = 1 + anmeldung.AnzahlBegleitpersonen;
                foreach (var tagId in GueltigeTage(modus, tage, anmeldung.TagIds))
                    belegung[tagId] += personen;
            }

            return belegung;
        }

        /// <summary>Freie Plätze je nicht abgesagtem Tag; null = unbegrenzt. Nie negativ.</summary>
        public static IReadOnlyDictionary<int, int?> FreiePlaetzeProTag(
            Teilnahmemodus modus,
            IReadOnlyCollection<TagKapazitaet> tage,
            IEnumerable<AnmeldungBelegung> anmeldungen,
            DateTime jetztUtc,
            int? ohneAnmeldungId = null)
        {
            var belegung = BelegungProTag(modus, tage, anmeldungen, jetztUtc, ohneAnmeldungId);
            return tage.Where(t => !t.Abgesagt).ToDictionary(
                t => t.TagId,
                t => t.MaxTeilnehmer is { } max ? Math.Max(0, max - belegung[t.TagId]) : (int?)null);
        }

        /// <summary>
        /// Prüft, ob eine (neue oder geänderte) Anmeldung mit dieser Personenzahl an allen ihren Tagen Platz hat.
        /// Abgesagte oder unbekannte Tag-Ids in angefragteTagIds werden ignoriert; der Aufrufer muss die Auswahl vorher prüfen.
        /// </summary>
        public static KapazitaetsPruefung Pruefen(
            Teilnahmemodus modus,
            IReadOnlyCollection<TagKapazitaet> tage,
            IEnumerable<AnmeldungBelegung> anmeldungen,
            DateTime jetztUtc,
            IReadOnlyCollection<int> angefragteTagIds,
            int personen,
            int? ohneAnmeldungId = null)
        {
            var frei = FreiePlaetzeProTag(modus, tage, anmeldungen, jetztUtc, ohneAnmeldungId);
            var volleTage = GueltigeTage(modus, tage, angefragteTagIds)
                .Where(tagId => frei[tagId] is { } freiePlaetze && freiePlaetze < personen)
                .OrderBy(tagId => tagId)
                .ToList();

            return new KapazitaetsPruefung(volleTage.Count == 0, volleTage);
        }
    }
}
