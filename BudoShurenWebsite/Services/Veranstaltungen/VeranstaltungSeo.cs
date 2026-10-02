using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using System.Text.Json;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Angaben für Suchmaschinen und Link-Vorschauen der öffentlichen Veranstaltungsseite. Alles wird aus den
    /// vorhandenen Feldern abgeleitet (Titel, Kurzbeschreibung, Termine, Ort, Bilder); eigene SEO-Felder braucht es nicht.
    /// </summary>
    public static class VeranstaltungSeo
    {
        public const string Veranstalter = "Budo Shuren Dojo e.V.";

        /// <summary>Öffentliche Adresse eines Bildes (Freigabe: ImageService.AllowAnonymous).</summary>
        public static string BildUrl(string basisUrl, int bildId) => $"{basisUrl}Account/Member/Filesave/GetImage/{bildId}";

        /// <summary>"Nur per Link" soll nicht in Suchmaschinen auftauchen.</summary>
        public static bool Indexieren(VeranstaltungAnzeige v) => v.Sichtbarkeit == VeranstaltungSichtbarkeit.Oeffentlich;

        /// <summary>
        /// schema.org/Event als JSON-LD: damit kann Google die Veranstaltung mit Datum und Ort in der Suche anzeigen.
        /// Die Serialisierung maskiert &lt; und &gt;, der Text kann also gefahrlos in ein script-Element.
        /// null ohne Termine.
        /// </summary>
        public static string? StrukturierteDaten(VeranstaltungAnzeige v, string basisUrl)
        {
            var aktive = TerminText.Sortiert(v.AktiveTage).ToList();
            var termine = aktive.Count > 0 ? aktive : TerminText.Sortiert(v.Tage).ToList();
            if (termine.Count == 0)
                return null;

            var (erster, letzter) = (termine[0], termine[^1]);
            var daten = new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "Event",
                ["name"] = v.Titel,
                ["url"] = VeranstaltungLinks.Veranstaltung(basisUrl, v.Slug),
                ["startDate"] = Zeitpunkt(erster.Datum.ToDateTime(erster.Beginn)),
                ["eventStatus"] = v.Status == VeranstaltungStatus.Abgesagt ? "https://schema.org/EventCancelled" : "https://schema.org/EventScheduled",
                ["eventAttendanceMode"] = "https://schema.org/OfflineEventAttendanceMode",
                ["organizer"] = new Dictionary<string, object> { ["@type"] = "Organization", ["name"] = Veranstalter, ["url"] = basisUrl }
            };

            if (letzter.Ende is { } ende)
                daten["endDate"] = Zeitpunkt(letzter.Datum.ToDateTime(ende));
            if (!string.IsNullOrWhiteSpace(v.Kurzbeschreibung))
                daten["description"] = v.Kurzbeschreibung;
            if (!string.IsNullOrWhiteSpace(v.Ort))
                daten["location"] = new Dictionary<string, object> { ["@type"] = "Place", ["name"] = v.Ort, ["address"] = v.Adresse ?? v.Ort };

            var bilder = v.BildIds.Select(id => BildUrl(basisUrl, id)).ToList();
            if (bilder.Count > 0)
                daten["image"] = bilder;

            return JsonSerializer.Serialize(daten);
        }

        private static string Zeitpunkt(DateTime ortszeit) => Ortszeit.MitVersatz(ortszeit).ToString("yyyy-MM-dd'T'HH:mm:sszzz");
    }
}
