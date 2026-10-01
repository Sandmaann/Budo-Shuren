using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    public enum AnmeldeErgebnisArt
    {
        /// <summary>
        /// Eine Mail ist unterwegs. Bewusst dasselbe Ergebnis für neue, bereits angemeldete und abgelehnte Adressen,
        /// damit sich über das Formular nicht herausfinden lässt, wer angemeldet ist.
        /// </summary>
        EmailVersendet,
        Ungueltig,
        NichtGefunden,
        NochNichtOffen,
        Geschlossen,
        Ausgebucht
    }

    public sealed record AnmeldeErgebnis(AnmeldeErgebnisArt Art, IReadOnlyDictionary<string, string> Fehler, IReadOnlyList<DateOnly> VolleTage)
    {
        private static readonly IReadOnlyDictionary<string, string> KeineFehler = new Dictionary<string, string>();

        public static AnmeldeErgebnis Von(AnmeldeErgebnisArt art) => new(art, KeineFehler, []);

        public static AnmeldeErgebnis Ungueltig(IReadOnlyDictionary<string, string> fehler) => new(AnmeldeErgebnisArt.Ungueltig, fehler, []);

        public static AnmeldeErgebnis Ausgebucht(IReadOnlyList<DateOnly> volleTage) => new(AnmeldeErgebnisArt.Ausgebucht, KeineFehler, volleTage);
    }

    public enum BestaetigungsErgebnis
    {
        Bestaetigt,
        /// <summary>Unbekannt, bereits verwendet oder die unbestätigte Anmeldung ist verfallen.</summary>
        LinkUngueltig,
        Ausgebucht,
        Geschlossen
    }

    public interface IAnmeldungService
    {
        /// <param name="basisUrl">Basis der Links in den Mails (NavigationManager.BaseUri, endet auf "/").</param>
        Task<AnmeldeErgebnis> AnmeldenAsync(string slug, AnmeldeEingabe eingabe, string basisUrl, CancellationToken abbruch = default);

        /// <summary>Nur lesend (für die Seite hinter dem Link): Titel der Veranstaltung, wenn der Link eine unbestätigte Anmeldung trifft.</summary>
        Task<string?> BestaetigungslinkPruefenAsync(string token, CancellationToken abbruch = default);

        /// <summary>Bestätigt die Anmeldung (Double-Opt-In). Nur nach ausdrücklichem Klick aufrufen (POST), nie beim Öffnen des Links.</summary>
        Task<BestaetigungsErgebnis> BestaetigenAsync(string token, string basisUrl, CancellationToken abbruch = default);
    }

    /// <summary>
    /// Öffentliche Anmeldung ohne Benutzerkonto. Pro Veranstaltung und Adresse gibt es einen Datensatz:
    /// bestehende Anmeldungen bekommen einen neuen Link, abgemeldete werden reaktiviert, abgelehnte bleiben gesperrt.
    /// Platzprüfung und Speichern laufen unter einer Sperre der Veranstaltung (VeranstaltungSperre).
    /// </summary>
    public sealed class AnmeldungService : IAnmeldungService
    {
        public const string BezugTyp = "Anmeldung";

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly IEmailWarteschlange _warteschlange;
        private readonly TimeProvider _zeit;
        private readonly VeranstaltungenOptionen _optionen;

        public AnmeldungService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            IEmailWarteschlange warteschlange,
            TimeProvider zeit,
            IOptions<VeranstaltungenOptionen> optionen)
        {
            _dbFactory = dbFactory;
            _warteschlange = warteschlange;
            _zeit = zeit;
            _optionen = optionen.Value;
        }

        private DateTime JetztUtc => _zeit.GetUtcNow().UtcDateTime;

        public async Task<AnmeldeErgebnis> AnmeldenAsync(string slug, AnmeldeEingabe eingabe, string basisUrl, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var v = await kontext.Veranstaltungen.Include(x => x.Tage).SingleOrDefaultAsync(x => x.Slug == slug, abbruch);
            if (v is null || v.Status != VeranstaltungStatus.Veroeffentlicht)
                return AnmeldeErgebnis.Von(AnmeldeErgebnisArt.NichtGefunden);

            var tage = v.Tage.ToList();
            switch (AnmeldeFenster.Zustand(v, tage, Ortszeit.Jetzt(_zeit)))
            {
                case AnmeldeZustand.NochNichtOffen:
                    return AnmeldeErgebnis.Von(AnmeldeErgebnisArt.NochNichtOffen);
                case AnmeldeZustand.Geschlossen:
                    return AnmeldeErgebnis.Von(AnmeldeErgebnisArt.Geschlossen);
            }

            var pruefung = AnmeldeValidierung.Pruefen(v, tage, eingabe, _optionen.MaxInfoEmails);
            if (!pruefung.IstGueltig)
                return AnmeldeErgebnis.Ungueltig(pruefung.Fehler);
            var daten = pruefung.Anmeldung!;

            await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            await VeranstaltungSperre.SetzenAsync(kontext, v.Id, abbruch);

            var anmeldungen = await kontext.Anmeldungen
                .Include(a => a.Tage)
                .Include(a => a.InfoEmails)
                .Where(a => a.VeranstaltungId == v.Id)
                .ToListAsync(abbruch);
            var vorhanden = anmeldungen.SingleOrDefault(a => a.Email == daten.Email);
            var jetzt = JetztUtc;

            // Abgelehnt: nichts ändern, neutrale Mail an die Adresse
            if (vorhanden is { Status: AnmeldungStatus.Abgelehnt })
            {
                Einreihen(kontext, v, vorhanden, VeranstaltungMailVorlagen.AnmeldungNichtMoeglich(v));
                return await AbschliessenAsync(kontext, transaktion, abbruch);
            }

            // Schon angemeldet: Daten bleiben (Änderungen über den Verwaltungslink), es kommt ein neuer Link
            if (vorhanden is { Status: AnmeldungStatus.Angemeldet or AnmeldungStatus.Warteliste })
            {
                var neuerLink = AnmeldeToken.Erzeugen();
                TokenSetzen(vorhanden, neuerLink, jetzt);
                EreignisHinzufuegen(vorhanden, AnmeldungEreignisArt.LinkVersendet, jetzt);
                Einreihen(kontext, v, vorhanden, VeranstaltungMailVorlagen.LinkErneut(v, vorhanden, VeranstaltungLinks.MeineAnmeldungUrl(basisUrl, neuerLink.Klartext), nochUnbestaetigt: false));
                return await AbschliessenAsync(kontext, transaktion, abbruch);
            }

            // Neu, abgemeldet oder noch unbestätigt: Daten übernehmen und Platz (neu) prüfen
            var kapazitaet = KapazitaetsRechner.Pruefen(
                v.Teilnahmemodus,
                tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList(),
                anmeldungen.Select(Belegung).ToList(),
                jetzt,
                daten.TagIds,
                1 + daten.AnzahlBegleitpersonen,
                vorhanden?.Id);
            if (!kapazitaet.Passt)
                return AnmeldeErgebnis.Ausgebucht(tage.Where(t => kapazitaet.VolleTagIds.Contains(t.Id)).Select(t => t.Datum).Order().ToList());

            var anmeldung = vorhanden ?? new Anmeldung { VeranstaltungId = v.Id, Quelle = AnmeldungQuelle.Formular, ErstelltUtc = jetzt };
            var ereignis = vorhanden?.Status switch
            {
                null => AnmeldungEreignisArt.Angelegt,
                AnmeldungStatus.Storniert => AnmeldungEreignisArt.Reaktiviert,
                _ => AnmeldungEreignisArt.DatenGeaendert
            };

            DatenUebernehmen(anmeldung, daten, jetzt);
            var token = AnmeldeToken.Erzeugen();
            TokenSetzen(anmeldung, token, jetzt);

            if (v.DoubleOptIn)
            {
                StatusSetzen(anmeldung, AnmeldungStatus.Unbestaetigt);
                anmeldung.ReserviertBisUtc = jetzt.AddHours(_optionen.ReservierungStunden);
            }
            else
            {
                StatusSetzen(anmeldung, AnmeldungStatus.Angemeldet);
                anmeldung.ReserviertBisUtc = null;
            }
            EreignisHinzufuegen(anmeldung, ereignis, jetzt);

            if (vorhanden is null)
                kontext.Anmeldungen.Add(anmeldung);
            // Erst speichern, damit die Mails den Bezug (Id) bekommen
            await kontext.SaveChangesAsync(abbruch);

            if (v.DoubleOptIn)
                Einreihen(kontext, v, anmeldung, VeranstaltungMailVorlagen.OptIn(v, tage, anmeldung, VeranstaltungLinks.BestaetigenUrl(basisUrl, token.Klartext), _optionen.ReservierungStunden));
            else
                BestaetigungsMailsEinreihen(kontext, v, tage, anmeldung, token, basisUrl, jetzt);

            return await AbschliessenAsync(kontext, transaktion, abbruch);
        }

        public async Task<string?> BestaetigungslinkPruefenAsync(string token, CancellationToken abbruch = default)
        {
            if (AnmeldeToken.Hash(token) is not { } hash)
                return null;

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            return await kontext.Anmeldungen.AsNoTracking()
                .Where(a => a.TokenHash == hash && a.Status == AnmeldungStatus.Unbestaetigt)
                .Select(a => a.Veranstaltung!.Titel)
                .SingleOrDefaultAsync(abbruch);
        }

        public async Task<BestaetigungsErgebnis> BestaetigenAsync(string token, string basisUrl, CancellationToken abbruch = default)
        {
            if (AnmeldeToken.Hash(token) is not { } hash)
                return BestaetigungsErgebnis.LinkUngueltig;

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var veranstaltungId = await kontext.Anmeldungen
                .Where(a => a.TokenHash == hash)
                .Select(a => (int?)a.VeranstaltungId)
                .SingleOrDefaultAsync(abbruch);
            if (veranstaltungId is null)
                return BestaetigungsErgebnis.LinkUngueltig;

            await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            await VeranstaltungSperre.SetzenAsync(kontext, veranstaltungId.Value, abbruch);

            // Unter der Sperre neu laden: der Link könnte inzwischen benutzt worden sein
            var v = await kontext.Veranstaltungen.Include(x => x.Tage).SingleAsync(x => x.Id == veranstaltungId, abbruch);
            var anmeldungen = await kontext.Anmeldungen
                .Include(a => a.Tage)
                .Include(a => a.InfoEmails)
                .Where(a => a.VeranstaltungId == v.Id)
                .ToListAsync(abbruch);
            var anmeldung = anmeldungen.SingleOrDefault(a => a.TokenHash.SequenceEqual(hash));
            if (anmeldung is not { Status: AnmeldungStatus.Unbestaetigt })
                return BestaetigungsErgebnis.LinkUngueltig;
            if (v.Status != VeranstaltungStatus.Veroeffentlicht)
                return BestaetigungsErgebnis.Geschlossen;

            var jetzt = JetztUtc;
            var tage = v.Tage.ToList();

            // Reservierung abgelaufen: nur bestätigen, wenn noch Platz ist
            if (!(anmeldung.ReserviertBisUtc > jetzt))
            {
                var kapazitaet = KapazitaetsRechner.Pruefen(
                    v.Teilnahmemodus,
                    tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList(),
                    anmeldungen.Select(Belegung).ToList(),
                    jetzt,
                    anmeldung.Tage.Select(t => t.VeranstaltungsTagId).ToList(),
                    1 + anmeldung.AnzahlBegleitpersonen,
                    anmeldung.Id);
                if (!kapazitaet.Passt)
                    return BestaetigungsErgebnis.Ausgebucht;
            }

            StatusSetzen(anmeldung, AnmeldungStatus.Angemeldet);
            anmeldung.EmailBestaetigtUtc = jetzt;
            anmeldung.ReserviertBisUtc = null;
            anmeldung.GeaendertUtc = jetzt;
            // Der Bestätigungslink gilt nur einmal; für die Verwaltung gibt es einen neuen
            var verwaltungsLink = AnmeldeToken.Erzeugen();
            TokenSetzen(anmeldung, verwaltungsLink, jetzt);
            EreignisHinzufuegen(anmeldung, AnmeldungEreignisArt.Bestaetigt, jetzt);

            BestaetigungsMailsEinreihen(kontext, v, tage, anmeldung, verwaltungsLink, basisUrl, jetzt);
            await kontext.SaveChangesAsync(abbruch);
            await transaktion.CommitAsync(abbruch);
            _warteschlange.VersandAnstossen();
            return BestaetigungsErgebnis.Bestaetigt;
        }

        // ---------------------------------------------------------------------------------------------

        private static AnmeldungBelegung Belegung(Anmeldung a) =>
            new(a.Id, a.Status, a.ReserviertBisUtc, a.AnzahlBegleitpersonen, a.Tage.Select(t => t.VeranstaltungsTagId).ToList());

        private static void StatusSetzen(Anmeldung anmeldung, AnmeldungStatus neu)
        {
            // Neue Anmeldungen starten als Unbestaetigt; gleicher Status ist kein Wechsel
            if (anmeldung.Status != neu)
                AnmeldungStatusUebergaenge.Pruefen(anmeldung.Status, neu, EreignisAkteur.Teilnehmer);
            anmeldung.Status = neu;
            anmeldung.StatusGrund = null;
        }

        private static void TokenSetzen(Anmeldung anmeldung, AnmeldeTokenPaar token, DateTime jetzt)
        {
            anmeldung.TokenHash = token.Hash;
            anmeldung.TokenErstelltUtc = jetzt;
        }

        private static void EreignisHinzufuegen(Anmeldung anmeldung, AnmeldungEreignisArt art, DateTime jetzt) =>
            anmeldung.Ereignisse.Add(new AnmeldungEreignis { ZeitpunktUtc = jetzt, Akteur = EreignisAkteur.Teilnehmer, Art = art });

        private static void DatenUebernehmen(Anmeldung anmeldung, GepruefteAnmeldung daten, DateTime jetzt)
        {
            anmeldung.Email = daten.Email;
            anmeldung.Vorname = daten.Vorname;
            anmeldung.Nachname = daten.Nachname;
            anmeldung.Telefon = daten.Telefon;
            anmeldung.Verein = daten.Verein;
            anmeldung.Graduierung = daten.Graduierung;
            anmeldung.Bemerkung = daten.Bemerkung;
            anmeldung.AnzahlBegleitpersonen = daten.AnzahlBegleitpersonen;
            anmeldung.DatenschutzAkzeptiertUtc = jetzt;
            if (anmeldung.Id != 0)
                anmeldung.GeaendertUtc = jetzt;

            // Abgleichen statt leeren und neu anlegen: sonst gäbe es denselben Schlüssel gelöscht und neu im selben SaveChanges
            foreach (var alt in anmeldung.Tage.Where(t => !daten.TagIds.Contains(t.VeranstaltungsTagId)).ToList())
                anmeldung.Tage.Remove(alt);
            foreach (var tagId in daten.TagIds.Where(id => anmeldung.Tage.All(t => t.VeranstaltungsTagId != id)))
                anmeldung.Tage.Add(new AnmeldungTag { VeranstaltungsTagId = tagId });

            // Bestehende Info-Adressen behalten (inkl. Abmeldung), fehlende entfernen, neue anlegen.
            // Das Abmeldetoken wird erst beim Versand der Info-Mail erzeugt; bis dahin ein zufälliger Platzhalter.
            foreach (var alt in anmeldung.InfoEmails.Where(i => !daten.InfoEmails.Contains(i.Email)).ToList())
                anmeldung.InfoEmails.Remove(alt);
            foreach (var adresse in daten.InfoEmails.Where(a => anmeldung.InfoEmails.All(i => i.Email != a)))
                anmeldung.InfoEmails.Add(new AnmeldungInfoEmail { Email = adresse, AbmeldeTokenHash = AnmeldeToken.Erzeugen().Hash });
        }

        /// <summary>Bestätigung mit Verwaltungslink an den Anmelder, kurze Info an jede (nicht abgemeldete) Info-Adresse.</summary>
        private void BestaetigungsMailsEinreihen(
            ApplicationDbContext kontext, Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, Anmeldung anmeldung,
            AnmeldeTokenPaar verwaltungsLink, string basisUrl, DateTime jetzt)
        {
            Einreihen(kontext, v, anmeldung, VeranstaltungMailVorlagen.Bestaetigung(v, tage, anmeldung, VeranstaltungLinks.MeineAnmeldungUrl(basisUrl, verwaltungsLink.Klartext)));

            foreach (var info in anmeldung.InfoEmails.Where(i => i.AbgemeldetUtc is null))
            {
                var abmelden = AnmeldeToken.Erzeugen();
                info.AbmeldeTokenHash = abmelden.Hash;
                var inhalt = VeranstaltungMailVorlagen.InfoAnBegleitung(
                    v, tage, anmeldung, VeranstaltungLinks.Veranstaltung(basisUrl, v.Slug), VeranstaltungLinks.InfoAbmeldenUrl(basisUrl, abmelden.Klartext));
                _warteschlange.Hinzufuegen(kontext, new AusgehendeEmail(info.Email, inhalt.Betreff, inhalt.Html)
                {
                    AntwortAn = v.KontaktEmail,
                    Prioritaet = EmailPrioritaet.Normal,
                    BezugTyp = BezugTyp,
                    BezugId = anmeldung.Id
                });
            }
        }

        /// <summary>Mail an den Anmelder, mit hoher Priorität (Links sollen sofort ankommen).</summary>
        private void Einreihen(ApplicationDbContext kontext, Veranstaltung v, Anmeldung anmeldung, MailInhalt inhalt) =>
            _warteschlange.Hinzufuegen(kontext, new AusgehendeEmail(anmeldung.Email, inhalt.Betreff, inhalt.Html)
            {
                AntwortAn = v.KontaktEmail,
                Prioritaet = EmailPrioritaet.Hoch,
                BezugTyp = BezugTyp,
                BezugId = anmeldung.Id == 0 ? null : anmeldung.Id
            });

        private async Task<AnmeldeErgebnis> AbschliessenAsync(
            ApplicationDbContext kontext, IDbContextTransaction transaktion, CancellationToken abbruch)
        {
            await kontext.SaveChangesAsync(abbruch);
            await transaktion.CommitAsync(abbruch);
            _warteschlange.VersandAnstossen();
            return AnmeldeErgebnis.Von(AnmeldeErgebnisArt.EmailVersendet);
        }
    }
}
