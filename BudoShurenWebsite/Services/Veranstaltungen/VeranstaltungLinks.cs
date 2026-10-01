namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Öffentliche Adressen des Moduls an einer Stelle (Seiten, Mails, Maskierung in Logs).
    /// Die Basis-URL kommt aus der Anfrage (NavigationManager.BaseUri) und endet auf "/".
    /// </summary>
    public static class VeranstaltungLinks
    {
        public const string Basis = "veranstaltungen";
        public const string Bestaetigen = "veranstaltungen/bestaetigen";
        public const string MeineAnmeldung = "veranstaltungen/meine-anmeldung";
        public const string InfoAbmelden = "veranstaltungen/info-abmelden";
        public const string BenachrichtigungAbmelden = "veranstaltungen/benachrichtigung-abmelden";
        public const string EmailBestaetigen = "veranstaltungen/email-bestaetigen";
        public const string LinkAnfordern = "veranstaltungen/link-anfordern";

        /// <summary>Pfade, deren letztes Segment ein geheimes Token ist.</summary>
        public static readonly string[] PfadeMitToken = [Bestaetigen, MeineAnmeldung, InfoAbmelden, BenachrichtigungAbmelden, EmailBestaetigen];

        public static string Veranstaltung(string basisUrl, string slug) => $"{basisUrl}{Basis}/{Uri.EscapeDataString(slug)}";

        public static string BestaetigenUrl(string basisUrl, string token) => $"{basisUrl}{Bestaetigen}/{token}";

        public static string MeineAnmeldungUrl(string basisUrl, string token) => $"{basisUrl}{MeineAnmeldung}/{token}";

        public static string InfoAbmeldenUrl(string basisUrl, string token) => $"{basisUrl}{InfoAbmelden}/{token}";

        public static string BenachrichtigungAbmeldenUrl(string basisUrl, string token) => $"{basisUrl}{BenachrichtigungAbmelden}/{token}";

        public static string EmailBestaetigenUrl(string basisUrl, string token) => $"{basisUrl}{EmailBestaetigen}/{token}";

        public static string LinkAnfordernUrl(string basisUrl) => $"{basisUrl}{LinkAnfordern}";

        /// <summary>Ersetzt das Token in einem Pfad durch "***", damit es nicht in Logs landet.</summary>
        public static string OhneToken(string? pfad)
        {
            if (string.IsNullOrEmpty(pfad))
                return string.Empty;

            foreach (var praefix in PfadeMitToken)
            {
                var start = pfad.IndexOf(praefix + "/", StringComparison.OrdinalIgnoreCase);
                if (start >= 0)
                    return pfad[..(start + praefix.Length + 1)] + "***";
            }
            return pfad;
        }

        /// <summary>Für Token-Seiten: kein Referrer an fremde Seiten, nicht cachen, nicht indexieren.</summary>
        public static void SicherheitsHeaderSetzen(HttpResponse antwort)
        {
            if (antwort.HasStarted)
                return;
            antwort.Headers["Referrer-Policy"] = "no-referrer";
            antwort.Headers.CacheControl = "no-store";
            antwort.Headers["X-Robots-Tag"] = "noindex, nofollow";
        }
    }
}
