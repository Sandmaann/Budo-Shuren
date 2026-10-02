namespace BudoShurenWebsite.Global
{
    /// <summary>
    /// Umrechnung zwischen UTC und der Ortszeit des Vereins (Europe/Berlin), unabhängig von der Zeitzone des Servers.
    /// Fachliche Zeitangaben (Termine, Fristen) sind Ortszeit, technische Zeitstempel ("...Utc") UTC.
    /// </summary>
    public static class Ortszeit
    {
        // IANA-Id: funktioniert unter Windows (ICU, .NET 6+) und Linux gleichermaßen
        public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        public static DateTime Jetzt(TimeProvider zeit) => AusUtc(zeit.GetUtcNow().UtcDateTime);

        public static DateTime AusUtc(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

        /// <summary>Ortszeit mit ihrem UTC-Versatz, z. B. für strukturierte Daten ("2026-11-14T10:00:00+01:00").</summary>
        public static DateTimeOffset MitVersatz(DateTime ortszeit)
        {
            var utc = NachUtc(ortszeit);
            return new DateTimeOffset(utc).ToOffset(Zone.GetUtcOffset(utc));
        }

        /// <summary>
        /// Rechnet eine Ortszeit nach UTC um. Bei der Zeitumstellung im Herbst gibt es eine Stunde doppelt;
        /// dann wird die Standardzeit (Winterzeit) angenommen. Nicht existierende Zeiten (Frühjahr) werden um eine Stunde verschoben.
        /// </summary>
        public static DateTime NachUtc(DateTime ortszeit)
        {
            var unbestimmt = DateTime.SpecifyKind(ortszeit, DateTimeKind.Unspecified);
            if (Zone.IsInvalidTime(unbestimmt))
                unbestimmt = unbestimmt.AddHours(1);
            return TimeZoneInfo.ConvertTimeToUtc(unbestimmt, Zone);
        }
    }
}
