using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using System.Text.Json;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Macht ein gespeichertes Ereignis (Art + DetailsJson aus EreignisDiff) für Organisatoren lesbar,
    /// z. B. "Vorname: Max → Moritz". Unbekannte oder kaputte Details fallen auf den Namen der Art zurück.
    /// </summary>
    public static class EreignisText
    {
        private const string Leer = "–";

        /// <summary>Felder, die nicht in Organisator-Benachrichtigungen stehen (die auch an externe Adressen gehen).</summary>
        private static readonly HashSet<string> Vertraulich = ["Telefon", "Bemerkung"];

        public static string Beschreiben(AnmeldungEreignisArt art, string? detailsJson) => Beschreiben(art, detailsJson, datensparsam: false);

        /// <summary>
        /// Für Organisator-Benachrichtigungen (Plan 2.9): ohne Telefon, Bemerkung, Ablehnungsgrund und E-Mail-Adressen,
        /// nur die Art der Änderung bzw. Name, Personen und Tage.
        /// </summary>
        public static string FuerBenachrichtigung(AnmeldungEreignisArt art, string? detailsJson) => art switch
        {
            AnmeldungEreignisArt.EmailGeaendert => "E-Mail-Adresse geändert",
            AnmeldungEreignisArt.InfoEmailsGeaendert => "Info-Adressen geändert",
            AnmeldungEreignisArt.Abgelehnt => art.Beschreibung(),
            _ => Beschreiben(art, detailsJson, datensparsam: true)
        };

        private static string Beschreiben(AnmeldungEreignisArt art, string? detailsJson, bool datensparsam)
        {
            if (string.IsNullOrWhiteSpace(detailsJson))
                return art.Beschreibung();

            try
            {
                using var dokument = JsonDocument.Parse(detailsJson);
                var details = dokument.RootElement;
                var text = art switch
                {
                    AnmeldungEreignisArt.DatenGeaendert or AnmeldungEreignisArt.AdminBearbeitet => Felder(details, datensparsam),
                    AnmeldungEreignisArt.BegleitungGeaendert => $"Begleitpersonen: {Wert(details, "Alt")} → {Wert(details, "Neu")}",
                    AnmeldungEreignisArt.TageGeaendert => $"Tage: {Liste(details, "Alt")} → {Liste(details, "Neu")}",
                    AnmeldungEreignisArt.InfoEmailsGeaendert => InfoAdressen(details),
                    AnmeldungEreignisArt.EmailGeaendert => $"E-Mail: {Wert(details, "Alt")} → {Wert(details, "Neu")}",
                    AnmeldungEreignisArt.TagAbgesagt => $"Tag abgesagt: {Wert(details, "Alt")}",
                    AnmeldungEreignisArt.Abgelehnt => details.TryGetProperty("Grund", out var grund) && grund.ValueKind == JsonValueKind.String
                        ? $"Abgelehnt: {grund.GetString()}"
                        : null,
                    _ => null
                };
                return string.IsNullOrWhiteSpace(text) ? art.Beschreibung() : text;
            }
            catch (JsonException)
            {
                return art.Beschreibung();
            }
        }

        // {"Vorname":{"Alt":"Max","Neu":"Moritz"}, ...}
        private static string? Felder(JsonElement details, bool datensparsam) =>
            details.ValueKind != JsonValueKind.Object
                ? null
                : string.Join("; ", details.EnumerateObject()
                    .Where(f => f.Value.ValueKind == JsonValueKind.Object && !(datensparsam && Vertraulich.Contains(f.Name)))
                    .Select(f => $"{f.Name}: {Wert(f.Value, "Alt")} → {Wert(f.Value, "Neu")}"));

        // Liste ({"Alt":[...],"Neu":[...]}) oder einzelne Abmeldung einer Info-Adresse ({"Alt":"x@y","Neu":null})
        private static string InfoAdressen(JsonElement details)
        {
            if (details.TryGetProperty("Alt", out var alt) && alt.ValueKind == JsonValueKind.String
                && details.TryGetProperty("Neu", out var neu) && neu.ValueKind == JsonValueKind.Null)
            {
                return $"{alt.GetString()} möchte keine Infos mehr";
            }
            return $"Info-Adressen: {Liste(details, "Alt")} → {Liste(details, "Neu")}";
        }

        private static string Wert(JsonElement element, string name) =>
            element.TryGetProperty(name, out var wert) && wert.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(wert.GetString())
                ? wert.GetString()!
                : Leer;

        private static string Liste(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var liste) || liste.ValueKind != JsonValueKind.Array)
                return Leer;
            var werte = liste.EnumerateArray().Where(w => w.ValueKind == JsonValueKind.String).Select(w => w.GetString()).ToList();
            return werte.Count == 0 ? Leer : string.Join(", ", werte);
        }
    }
}
