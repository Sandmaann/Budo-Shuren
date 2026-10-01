using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Die vom Teilnehmer änderbaren Daten einer Anmeldung zu einem Zeitpunkt (für den Vergleich vorher/nachher).</summary>
    public sealed record AnmeldungStand(
        string Vorname,
        string Nachname,
        string? Telefon,
        string? Verein,
        string? Graduierung,
        string? Bemerkung,
        int AnzahlBegleitpersonen,
        IReadOnlyList<DateOnly> Tage,
        IReadOnlyList<string> InfoEmails)
    {
        /// <param name="tage">Alle Tage der Veranstaltung, um gebuchte Tag-Ids in Daten umzurechnen.</param>
        public static AnmeldungStand Von(Anmeldung a, IReadOnlyCollection<VeranstaltungsTag> tage)
        {
            var gebucht = a.Tage.Select(t => t.VeranstaltungsTagId).ToHashSet();
            return new AnmeldungStand(
                a.Vorname,
                a.Nachname,
                a.Telefon,
                a.Verein,
                a.Graduierung,
                a.Bemerkung,
                a.AnzahlBegleitpersonen,
                tage.Where(t => gebucht.Contains(t.Id)).Select(t => t.Datum).Order().ToList(),
                a.InfoEmails.Select(i => i.Email).Order().ToList());
        }
    }

    public sealed record EreignisAenderung(AnmeldungEreignisArt Art, string DetailsJson);

    /// <summary>
    /// Vergleicht zwei Stände und liefert je Art der Änderung ein Ereignis mit altem und neuem Wert als JSON.
    /// Grundlage für die Historie in der Übersicht und für die Organisator-Benachrichtigungen.
    /// </summary>
    public static class EreignisDiff
    {
        private static readonly JsonSerializerOptions Json = new()
        {
            // Lesbar in der Datenbank; angezeigt wird es über Blazor (kodiert) bzw. kodiert in Mails
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static IReadOnlyList<EreignisAenderung> Erstellen(AnmeldungStand alt, AnmeldungStand neu)
        {
            var ergebnis = new List<EreignisAenderung>();

            var daten = new Dictionary<string, WertAenderung>();
            Vergleiche(daten, "Vorname", alt.Vorname, neu.Vorname);
            Vergleiche(daten, "Nachname", alt.Nachname, neu.Nachname);
            Vergleiche(daten, "Telefon", alt.Telefon, neu.Telefon);
            Vergleiche(daten, "Verein", alt.Verein, neu.Verein);
            Vergleiche(daten, "Graduierung", alt.Graduierung, neu.Graduierung);
            Vergleiche(daten, "Bemerkung", alt.Bemerkung, neu.Bemerkung);
            if (daten.Count > 0)
                ergebnis.Add(new EreignisAenderung(AnmeldungEreignisArt.DatenGeaendert, JsonSerializer.Serialize(daten, Json)));

            if (alt.AnzahlBegleitpersonen != neu.AnzahlBegleitpersonen)
                ergebnis.Add(new EreignisAenderung(AnmeldungEreignisArt.BegleitungGeaendert,
                    JsonSerializer.Serialize(new WertAenderung(alt.AnzahlBegleitpersonen.ToString(), neu.AnzahlBegleitpersonen.ToString()), Json)));

            if (!alt.Tage.SequenceEqual(neu.Tage))
                ergebnis.Add(new EreignisAenderung(AnmeldungEreignisArt.TageGeaendert,
                    JsonSerializer.Serialize(new ListenAenderung(Datumsliste(alt.Tage), Datumsliste(neu.Tage)), Json)));

            if (!alt.InfoEmails.SequenceEqual(neu.InfoEmails))
                ergebnis.Add(new EreignisAenderung(AnmeldungEreignisArt.InfoEmailsGeaendert,
                    JsonSerializer.Serialize(new ListenAenderung(alt.InfoEmails, neu.InfoEmails), Json)));

            return ergebnis;
        }

        /// <summary>Für einen einzelnen Wert, z. B. die E-Mail-Adresse.</summary>
        public static string Wert(string? alt, string? neu) => JsonSerializer.Serialize(new WertAenderung(alt, neu), Json);

        private static void Vergleiche(Dictionary<string, WertAenderung> daten, string feld, string? alt, string? neu)
        {
            if (!string.Equals(alt ?? "", neu ?? "", StringComparison.Ordinal))
                daten[feld] = new WertAenderung(alt, neu);
        }

        private static IReadOnlyList<string> Datumsliste(IEnumerable<DateOnly> tage) => tage.Select(t => t.ToString("dd.MM.yyyy")).ToList();

        private sealed record WertAenderung(string? Alt, string? Neu);

        private sealed record ListenAenderung(IReadOnlyList<string> Alt, IReadOnlyList<string> Neu);
    }
}
