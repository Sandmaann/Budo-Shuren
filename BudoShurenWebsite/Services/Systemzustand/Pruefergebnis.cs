using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>Gemeinsame Bausteine der Prüfungen.</summary>
    internal static class Pruefergebnis
    {
        /// <summary>
        /// Spielraum für Prüfungen, die nachsehen, ob ein Hintergrundjob seine Arbeit getan hat: der Job läuft höchstens
        /// stündlich, erst wenn etwas deutlich länger liegt, ist das ein Befund.
        /// </summary>
        public static readonly TimeSpan JobToleranz = TimeSpan.FromHours(3);

        /// <summary>Höchstens so viele Beispiele (IDs, Titel) pro Befund, damit die Antwort klein bleibt.</summary>
        public const int MaxBeispiele = 20;

        public static readonly HealthCheckResult VeranstaltungenAus =
            HealthCheckResult.Healthy("Modul Veranstaltungen ausgeschaltet, nicht geprüft");

        /// <summary>Keine Befunde: gesund. Sonst "eingeschränkt" mit allen Befunden.</summary>
        public static HealthCheckResult Aus(IReadOnlyCollection<string> befunde, string allesInOrdnung, IReadOnlyDictionary<string, object>? daten = null) =>
            befunde.Count == 0
                ? HealthCheckResult.Healthy(allesInOrdnung, daten)
                : HealthCheckResult.Degraded(string.Join(" ", befunde), data: daten);

        public static string Liste(IEnumerable<string> eintraege) => string.Join(", ", eintraege.Take(MaxBeispiele));
    }
}
