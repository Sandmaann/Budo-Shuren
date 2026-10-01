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

        /// <summary>So lange hält eine unbestätigte Anmeldung ihre Plätze frei (Double-Opt-In).</summary>
        public int ReservierungStunden { get; set; } = 24;

        /// <summary>Höchstzahl Info-Adressen je Anmeldung (zusätzlich höchstens so viele wie Begleitpersonen).</summary>
        public int MaxInfoEmails { get; set; } = 5;

        /// <summary>
        /// Adresse der Website für Links in Mails, die ohne Anfrage entstehen (Hintergrund-Jobs, Mails an externe Empfänger).
        /// In der Entwicklung auf die lokale Adresse setzen.
        /// </summary>
        public string WebsiteUrl { get; set; } = "https://www.budo-shuren-dojo.de/";

        /// <summary>false schaltet Benachrichtigungs- und Wartungs-Job ab (z. B. in Tests, die die Jobs direkt aufrufen).</summary>
        public bool HintergrundJobsAktiviert { get; set; } = true;

        /// <summary>WebsiteUrl, immer mit "/" am Ende (wie NavigationManager.BaseUri).</summary>
        public string BasisUrl => WebsiteUrl.EndsWith('/') ? WebsiteUrl : WebsiteUrl + "/";

        /// <summary>Wird beim App-Start geprüft.</summary>
        public bool IstGueltig() =>
            ReservierungStunden > 0
            && MaxInfoEmails >= 0
            && Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var url)
            && url.Scheme is "http" or "https";
    }
}
