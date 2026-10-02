using BudoShurenWebsite.Models.Veranstaltungen;
using System.Globalization;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Einheitliche Texte für Termine auf allen Seiten, in Mails und Exporten.
    /// Ein Datum kann mehrere Termine haben (z. B. Training am Tag, Essen am Abend); Listen werden deshalb nach Datum gruppiert.
    /// </summary>
    public static class TerminText
    {
        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        // Fest statt "ddd": ICU und NLS kürzen unterschiedlich ab ("Sa." bzw. "Sa")
        private static readonly string[] Wochentage = ["So", "Mo", "Di", "Mi", "Do", "Fr", "Sa"];

        /// <summary>Chronologisch: Datum, Beginn, dann Titel (gleichzeitige Termine stabil sortiert).</summary>
        public static IEnumerable<T> Sortiert<T>(IEnumerable<T> termine) where T : ITermin =>
            termine.OrderBy(t => t.Datum).ThenBy(t => t.Beginn).ThenBy(t => t.Titel, StringComparer.Ordinal);

        /// <summary>Termine je Datum, chronologisch, für Listen mit dem Datum als Überschrift.</summary>
        public static IReadOnlyList<IGrouping<DateOnly, T>> NachDatum<T>(IEnumerable<T> termine) where T : ITermin =>
            Sortiert(termine).GroupBy(t => t.Datum).ToList();

        /// <summary>"Samstag, 14. November 2026".</summary>
        public static string Datum(DateOnly datum) => datum.ToString("dddd, d. MMMM yyyy", Deutsch);

        /// <summary>"10:00 – 17:00 Uhr" bzw. "ab 19:00 Uhr" bei offenem Ende.</summary>
        public static string Uhrzeit(ITermin termin) =>
            termin.Ende is { } ende
                ? $"{termin.Beginn:HH\\:mm} – {ende:HH\\:mm} Uhr"
                : $"ab {termin.Beginn:HH\\:mm} Uhr";

        /// <summary>Ein Termin in einer Zeile: "Samstag, 14. November 2026, 10:00 – 17:00 Uhr (Training)".</summary>
        public static string Zeile(ITermin termin) =>
            $"{Datum(termin.Datum)}, {Uhrzeit(termin)}" + (string.IsNullOrWhiteSpace(termin.Titel) ? "" : $" ({termin.Titel.Trim()})");

        /// <summary>
        /// Kurz und eindeutig für Tabellen, Exporte und Meldungen: "Sa 14.11.". Hat das Datum weitere Termine,
        /// kommt die Uhrzeit dazu ("Sa 14.11. 19:00"), bei gleicher Uhrzeit auch der Titel.
        /// </summary>
        /// <param name="alle">Alle Termine der Veranstaltung (einschließlich termin), um gleiche Daten zu erkennen.</param>
        /// <param name="mitTitel">Titel immer anhängen ("Sa 14.11. Training"): für Texte, die Teilnehmer oder Organisatoren lesen.
        /// Ohne nur in engen Tabellenköpfen und Meldungen auf der Bearbeiten-Seite, wo der Termin daneben steht.</param>
        public static string Kurz(ITermin termin, IEnumerable<ITermin> alle, bool mitTitel = false)
        {
            var text = $"{Wochentage[(int)termin.Datum.DayOfWeek]} {termin.Datum:dd.MM.}";
            var amSelbenTag = alle.Where(t => t.Datum == termin.Datum).ToList();
            if (amSelbenTag.Count > 1)
                text += $" {termin.Beginn:HH\\:mm}";

            var titelNoetig = mitTitel || amSelbenTag.Count(t => t.Beginn == termin.Beginn) > 1;
            return titelNoetig && !string.IsNullOrWhiteSpace(termin.Titel) ? $"{text} {termin.Titel.Trim()}" : text;
        }

        /// <summary>
        /// Zeitraum über mehrere Daten: "14. November 2026", "14.–15. November 2026",
        /// "30. Oktober – 1. November 2026" bzw. "30. Dezember 2026 – 2. Januar 2027". Leer ohne Daten.
        /// </summary>
        public static string Zeitraum(IEnumerable<DateOnly> daten)
        {
            var sortiert = daten.Order().ToList();
            if (sortiert.Count == 0)
                return string.Empty;

            var (von, bis) = (sortiert[0], sortiert[^1]);
            var ende = bis.ToString("d. MMMM yyyy", Deutsch);
            if (von == bis)
                return ende;
            if (von.Year != bis.Year)
                return $"{von.ToString("d. MMMM yyyy", Deutsch)} – {ende}";
            if (von.Month != bis.Month)
                return $"{von.ToString("d. MMMM", Deutsch)} – {ende}";
            return $"{von.Day}.–{ende}";
        }
    }
}
