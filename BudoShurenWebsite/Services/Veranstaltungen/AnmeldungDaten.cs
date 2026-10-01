using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Gemeinsame Änderungen an einer Anmeldung für Anmeldung, Self-Service und Verwaltung.</summary>
    public static class AnmeldungDaten
    {
        public static AnmeldungBelegung Belegung(Anmeldung a) =>
            new(a.Id, a.Status, a.ReserviertBisUtc, a.AnzahlBegleitpersonen, a.Tage.Select(t => t.VeranstaltungsTagId).ToList());

        /// <summary>Prüft den Wechsel (AnmeldungStatusUebergaenge); gleicher Status ist kein Wechsel.</summary>
        public static void StatusSetzen(Anmeldung anmeldung, AnmeldungStatus neu, EreignisAkteur akteur, string? grund = null)
        {
            if (anmeldung.Status != neu)
                AnmeldungStatusUebergaenge.Pruefen(anmeldung.Status, neu, akteur);
            anmeldung.Status = neu;
            anmeldung.StatusGrund = grund;
        }

        public static void TokenSetzen(Anmeldung anmeldung, AnmeldeTokenPaar token, DateTime jetzt)
        {
            anmeldung.TokenHash = token.Hash;
            anmeldung.TokenErstelltUtc = jetzt;
        }

        public static void EreignisHinzufuegen(Anmeldung anmeldung, AnmeldungEreignisArt art, EreignisAkteur akteur, DateTime jetzt, string? detailsJson = null, string? akteurUserId = null) =>
            anmeldung.Ereignisse.Add(new AnmeldungEreignis
            {
                ZeitpunktUtc = jetzt,
                Akteur = akteur,
                AkteurUserId = akteurUserId,
                Art = art,
                DetailsJson = detailsJson
            });

        /// <summary>
        /// Übernimmt die geprüften Daten außer der E-Mail-Adresse (die ändert sich nur nach Bestätigung).
        /// Tage und Info-Adressen werden abgeglichen statt neu angelegt: so gibt es keinen Schlüssel doppelt
        /// (gelöscht und neu) im selben SaveChanges, und abgemeldete Info-Adressen bleiben abgemeldet.
        /// </summary>
        public static void Uebernehmen(Anmeldung anmeldung, GepruefteAnmeldung daten, DateTime jetzt)
        {
            anmeldung.Vorname = daten.Vorname;
            anmeldung.Nachname = daten.Nachname;
            anmeldung.Telefon = daten.Telefon;
            anmeldung.Verein = daten.Verein;
            anmeldung.Graduierung = daten.Graduierung;
            anmeldung.Bemerkung = daten.Bemerkung;
            anmeldung.AnzahlBegleitpersonen = daten.AnzahlBegleitpersonen;
            if (anmeldung.Id != 0)
                anmeldung.GeaendertUtc = jetzt;

            foreach (var alt in anmeldung.Tage.Where(t => !daten.TagIds.Contains(t.VeranstaltungsTagId)).ToList())
                anmeldung.Tage.Remove(alt);
            foreach (var tagId in daten.TagIds.Where(id => anmeldung.Tage.All(t => t.VeranstaltungsTagId != id)))
                anmeldung.Tage.Add(new AnmeldungTag { VeranstaltungsTagId = tagId });

            // Das Abmeldetoken einer neuen Info-Adresse entsteht erst beim Versand der Info-Mail; bis dahin ein zufälliger Platzhalter
            foreach (var alt in anmeldung.InfoEmails.Where(i => !daten.InfoEmails.Contains(i.Email)).ToList())
                anmeldung.InfoEmails.Remove(alt);
            foreach (var adresse in daten.InfoEmails.Where(a => anmeldung.InfoEmails.All(i => i.Email != a)))
                anmeldung.InfoEmails.Add(new AnmeldungInfoEmail { Email = adresse, AbmeldeTokenHash = AnmeldeToken.Erzeugen().Hash });
        }
    }
}
