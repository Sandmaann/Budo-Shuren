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
    /// <summary>Was Teilnehmer über ihren Verwaltungslink sehen und ändern können.</summary>
    /// <param name="Tage">Freie Plätze aus Sicht dieser Anmeldung (die eigenen Plätze zählen als frei).</param>
    /// <param name="Daten">Vorbelegung für das Änderungsformular.</param>
    public sealed record MeineAnmeldungAnsicht(
        string Titel,
        string Slug,
        VeranstaltungStatus VeranstaltungStatus,
        string? KontaktName,
        string? KontaktEmail,
        Teilnahmemodus Teilnahmemodus,
        int MinTageBeiTeilanmeldung,
        int MaxBegleitpersonen,
        FormularFeldModus TelefonFeld,
        FormularFeldModus VereinFeld,
        FormularFeldModus GraduierungFeld,
        FormularFeldModus BemerkungFeld,
        IReadOnlyList<TagAnzeige> Tage,
        AnmeldungStatus Status,
        bool AenderungenMoeglich,
        bool AbmeldenMoeglich,
        string Email,
        string? NeueEmailAusstehend,
        AnmeldeEingabe Daten,
        IReadOnlyList<string> AbgemeldeteInfoEmails);

    public enum SelbstverwaltungErgebnisArt
    {
        Gespeichert,
        KeineAenderung,
        Abgemeldet,
        Ungueltig,
        Ausgebucht,
        /// <summary>Frist abgelaufen, Veranstaltung nicht mehr veröffentlicht oder Anmeldung nicht (mehr) aktiv.</summary>
        NichtMehrMoeglich,
        LinkUngueltig
    }

    public sealed record SelbstverwaltungErgebnis(
        SelbstverwaltungErgebnisArt Art,
        IReadOnlyDictionary<string, string> Fehler,
        IReadOnlyList<DateOnly> VolleTage,
        bool EmailWechselAngefordert = false)
    {
        private static readonly IReadOnlyDictionary<string, string> KeineFehler = new Dictionary<string, string>();

        public static SelbstverwaltungErgebnis Von(SelbstverwaltungErgebnisArt art, bool emailWechsel = false) => new(art, KeineFehler, [], emailWechsel);

        public static SelbstverwaltungErgebnis Ungueltig(IReadOnlyDictionary<string, string> fehler) => new(SelbstverwaltungErgebnisArt.Ungueltig, fehler, []);

        public static SelbstverwaltungErgebnis Ausgebucht(IReadOnlyList<DateOnly> tage) => new(SelbstverwaltungErgebnisArt.Ausgebucht, KeineFehler, tage);
    }

    public enum EmailWechselErgebnis
    {
        Bestaetigt,
        LinkUngueltig,
        /// <summary>Mit der neuen Adresse gibt es inzwischen eine andere Anmeldung zu dieser Veranstaltung.</summary>
        AdresseVergeben
    }

    public interface ISelbstverwaltungService
    {
        /// <summary>null, wenn der Link ungültig ist.</summary>
        Task<MeineAnmeldungAnsicht?> LadenAsync(string token, CancellationToken abbruch = default);

        /// <summary>Daten, Tage, Begleitpersonen und Info-Adressen ändern. Eine neue E-Mail-Adresse gilt erst nach Bestätigung.</summary>
        Task<SelbstverwaltungErgebnis> AendernAsync(string token, AnmeldeEingabe eingabe, string basisUrl, CancellationToken abbruch = default);

        Task<SelbstverwaltungErgebnis> AbmeldenAsync(string token, string basisUrl, CancellationToken abbruch = default);

        /// <summary>Nur lesend: die neue Adresse, wenn der Link zu einem offenen E-Mail-Wechsel gehört.</summary>
        Task<string?> EmailWechselPruefenAsync(string token, CancellationToken abbruch = default);

        Task<EmailWechselErgebnis> EmailWechselBestaetigenAsync(string token, string basisUrl, CancellationToken abbruch = default);

        /// <summary>
        /// Schickt alle aktiven Anmeldungen einer Adresse mit neuen Links in einer Mail. Liefert bewusst nichts zurück:
        /// die Seite antwortet immer gleich, damit niemand herausfinden kann, welche Adressen angemeldet sind.
        /// </summary>
        Task LinksAnfordernAsync(string? email, string basisUrl, CancellationToken abbruch = default);

        /// <summary>Nur lesend: Titel der Veranstaltung, wenn der Link zu einer noch nicht abgemeldeten Info-Adresse gehört.</summary>
        Task<string?> InfoAbmeldungPruefenAsync(string token, CancellationToken abbruch = default);

        /// <summary>true, wenn der Link gültig war (auch wenn die Adresse schon abgemeldet war).</summary>
        Task<bool> InfoAbmeldenAsync(string token, CancellationToken abbruch = default);
    }

    /// <summary>
    /// Self-Service über den Verwaltungslink. Ändernde Aktionen laufen unter der Sperre der Veranstaltung
    /// (Platzprüfung) und schreiben Ereignisse mit altem und neuem Wert (EreignisDiff).
    /// </summary>
    public sealed class SelbstverwaltungService : ISelbstverwaltungService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly AnmeldungMailVersand _mails;
        private readonly TimeProvider _zeit;
        private readonly VeranstaltungenOptionen _optionen;

        public SelbstverwaltungService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            AnmeldungMailVersand mails,
            TimeProvider zeit,
            IOptions<VeranstaltungenOptionen> optionen)
        {
            _dbFactory = dbFactory;
            _mails = mails;
            _zeit = zeit;
            _optionen = optionen.Value;
        }

        private DateTime JetztUtc => _zeit.GetUtcNow().UtcDateTime;

        public async Task<MeineAnmeldungAnsicht?> LadenAsync(string token, CancellationToken abbruch = default)
        {
            if (AnmeldeToken.Hash(token) is not { } hash)
                return null;

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var a = await kontext.Anmeldungen.AsNoTracking()
                .Include(x => x.Veranstaltung!).ThenInclude(v => v.Tage)
                .Include(x => x.Tage)
                .Include(x => x.InfoEmails)
                .SingleOrDefaultAsync(x => x.TokenHash == hash, abbruch);
            if (a?.Veranstaltung is not { } v)
                return null;

            var belegungen = await kontext.Anmeldungen.AsNoTracking()
                .Where(x => x.VeranstaltungId == v.Id)
                .Select(x => new AnmeldungBelegung(x.Id, x.Status, x.ReserviertBisUtc, x.AnzahlBegleitpersonen, x.Tage.Select(t => t.VeranstaltungsTagId).ToList()))
                .ToListAsync(abbruch);

            var tage = v.Tage.OrderBy(t => t.Datum).ToList();
            var frei = KapazitaetsRechner.FreiePlaetzeProTag(
                v.Teilnahmemodus, tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList(), belegungen, JetztUtc, ohneAnmeldungId: a.Id);
            var jetzt = Ortszeit.Jetzt(_zeit);

            return new MeineAnmeldungAnsicht(
                v.Titel,
                v.Slug,
                v.Status,
                v.KontaktName,
                v.KontaktEmail,
                v.Teilnahmemodus,
                v.MinTageBeiTeilanmeldung,
                v.MaxBegleitpersonen,
                v.TelefonFeld,
                v.VereinFeld,
                v.GraduierungFeld,
                v.BemerkungFeld,
                tage.Select(t => new TagAnzeige(t.Id, t.Datum, t.Beginn, t.Ende, t.Titel, t.Abgesagt, t.Abgesagt ? 0 : frei[t.Id])).ToList(),
                a.Status,
                a.Status == AnmeldungStatus.Angemeldet && AnmeldeFenster.AenderungenMoeglich(v, tage, jetzt),
                (a.Status is AnmeldungStatus.Angemeldet or AnmeldungStatus.Warteliste) && AnmeldeFenster.AbmeldenMoeglich(v, tage, jetzt),
                a.Email,
                a.NeueEmail,
                new AnmeldeEingabe
                {
                    Vorname = a.Vorname,
                    Nachname = a.Nachname,
                    Email = a.Email,
                    Telefon = a.Telefon,
                    Verein = a.Verein,
                    Graduierung = a.Graduierung,
                    Bemerkung = a.Bemerkung,
                    AnzahlBegleitpersonen = a.AnzahlBegleitpersonen,
                    TagIds = a.Tage.Select(t => t.VeranstaltungsTagId).ToList(),
                    InfoEmails = string.Join("\n", a.InfoEmails.Select(i => i.Email).Order()),
                    DatenschutzAkzeptiert = true
                },
                a.InfoEmails.Where(i => i.AbgemeldetUtc != null).Select(i => i.Email).Order().ToList());
        }

        public async Task<SelbstverwaltungErgebnis> AendernAsync(string token, AnmeldeEingabe eingabe, string basisUrl, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            await using var sitzung = await GesperrtLadenAsync(kontext, TokenArt.Verwaltung, token, abbruch);
            if (sitzung is null)
                return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.LinkUngueltig);
            var (v, tage, anmeldungen, a) = sitzung.Value;

            if (a.Status != AnmeldungStatus.Angemeldet || !AnmeldeFenster.AenderungenMoeglich(v, tage, Ortszeit.Jetzt(_zeit)))
                return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.NichtMehrMoeglich);

            // Die Einwilligung liegt schon vor; das Änderungsformular fragt sie nicht erneut ab
            eingabe.DatenschutzAkzeptiert = true;
            var pruefung = AnmeldeValidierung.Pruefen(v, tage, eingabe, _optionen.MaxInfoEmails);
            if (!pruefung.IstGueltig)
                return SelbstverwaltungErgebnis.Ungueltig(pruefung.Fehler);
            var daten = pruefung.Anmeldung!;

            var emailWechsel = daten.Email != a.Email;
            if (emailWechsel && anmeldungen.Any(x => x.Id != a.Id && x.Email == daten.Email))
                return SelbstverwaltungErgebnis.Ungueltig(new Dictionary<string, string>
                {
                    [nameof(AnmeldeEingabe.Email)] = "Mit dieser Adresse gibt es für diese Veranstaltung bereits eine Anmeldung."
                });

            var jetzt = JetztUtc;
            var kapazitaet = KapazitaetsRechner.Pruefen(
                v.Teilnahmemodus,
                tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList(),
                anmeldungen.Select(AnmeldungDaten.Belegung).ToList(),
                jetzt,
                daten.TagIds,
                1 + daten.AnzahlBegleitpersonen,
                a.Id);
            if (!kapazitaet.Passt)
                return SelbstverwaltungErgebnis.Ausgebucht(tage.Where(t => kapazitaet.VolleTagIds.Contains(t.Id)).Select(t => t.Datum).Order().ToList());

            var alt = AnmeldungStand.Von(a, tage);
            var alteInfoEmails = a.InfoEmails.Select(i => i.Email).ToHashSet();
            AnmeldungDaten.Uebernehmen(a, daten, jetzt);
            var aenderungen = EreignisDiff.Erstellen(alt, AnmeldungStand.Von(a, tage));

            if (aenderungen.Count == 0 && !emailWechsel)
                return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.KeineAenderung);

            foreach (var aenderung in aenderungen)
                AnmeldungDaten.EreignisHinzufuegen(a, aenderung.Art, EreignisAkteur.Teilnehmer, jetzt, aenderung.DetailsJson);

            if (emailWechsel)
            {
                var wechselToken = AnmeldeToken.Erzeugen();
                a.NeueEmail = daten.Email;
                a.NeueEmailTokenHash = wechselToken.Hash;
                _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.EmailWechselBestaetigen(v, a, VeranstaltungLinks.EmailBestaetigenUrl(basisUrl, wechselToken.Klartext)), an: daten.Email);
                _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.EmailWechselHinweis(v, a, daten.Email));
            }

            if (aenderungen.Count > 0)
                _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.AenderungGespeichert(v, tage, a), EmailPrioritaet.Normal);

            var neueInfoEmails = a.InfoEmails.Select(i => i.Email).Where(e => !alteInfoEmails.Contains(e)).ToList();
            if (neueInfoEmails.Count > 0)
                _mails.InfoMails(kontext, v, tage, a, basisUrl, neueInfoEmails);

            await AbschliessenAsync(kontext, sitzung, abbruch);
            return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.Gespeichert, emailWechsel);
        }

        public async Task<SelbstverwaltungErgebnis> AbmeldenAsync(string token, string basisUrl, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            await using var sitzung = await GesperrtLadenAsync(kontext, TokenArt.Verwaltung, token, abbruch);
            if (sitzung is null)
                return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.LinkUngueltig);
            var (v, tage, _, a) = sitzung.Value;

            // Doppelt abschicken (z. B. Neuladen) ist kein Fehler
            if (a.Status == AnmeldungStatus.Storniert)
                return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.Abgemeldet);
            if (a.Status is not (AnmeldungStatus.Angemeldet or AnmeldungStatus.Warteliste) || !AnmeldeFenster.AbmeldenMoeglich(v, tage, Ortszeit.Jetzt(_zeit)))
                return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.NichtMehrMoeglich);

            var jetzt = JetztUtc;
            AnmeldungDaten.StatusSetzen(a, AnmeldungStatus.Storniert, EreignisAkteur.Teilnehmer);
            a.GeaendertUtc = jetzt;
            AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.Storniert, EreignisAkteur.Teilnehmer, jetzt);
            _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.AbmeldungBestaetigt(v, a, VeranstaltungLinks.Veranstaltung(basisUrl, v.Slug)));

            await AbschliessenAsync(kontext, sitzung, abbruch);
            return SelbstverwaltungErgebnis.Von(SelbstverwaltungErgebnisArt.Abgemeldet);
        }

        public async Task<string?> EmailWechselPruefenAsync(string token, CancellationToken abbruch = default)
        {
            if (AnmeldeToken.Hash(token) is not { } hash)
                return null;

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            return await kontext.Anmeldungen.AsNoTracking()
                .Where(a => a.NeueEmailTokenHash == hash && a.NeueEmail != null)
                .Select(a => a.NeueEmail)
                .SingleOrDefaultAsync(abbruch);
        }

        public async Task<EmailWechselErgebnis> EmailWechselBestaetigenAsync(string token, string basisUrl, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            await using var sitzung = await GesperrtLadenAsync(kontext, TokenArt.EmailWechsel, token, abbruch);
            if (sitzung is null)
                return EmailWechselErgebnis.LinkUngueltig;
            var (v, _, anmeldungen, a) = sitzung.Value;

            if (a.NeueEmail is not { } neueEmail || a.Status == AnmeldungStatus.Abgelehnt)
                return EmailWechselErgebnis.LinkUngueltig;
            if (anmeldungen.Any(x => x.Id != a.Id && x.Email == neueEmail))
                return EmailWechselErgebnis.AdresseVergeben;

            var jetzt = JetztUtc;
            var alteEmail = a.Email;
            a.Email = neueEmail;
            a.NeueEmail = null;
            a.NeueEmailTokenHash = null;
            a.GeaendertUtc = jetzt;
            // Der alte Verwaltungslink lag im Postfach der alten Adresse und gilt nicht mehr
            var verwaltungsLink = AnmeldeToken.Erzeugen();
            AnmeldungDaten.TokenSetzen(a, verwaltungsLink, jetzt);
            AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.EmailGeaendert, EreignisAkteur.Teilnehmer, jetzt, EreignisDiff.Wert(alteEmail, neueEmail));
            _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.EmailWechselAbgeschlossen(v, a, VeranstaltungLinks.MeineAnmeldungUrl(basisUrl, verwaltungsLink.Klartext)));

            await AbschliessenAsync(kontext, sitzung, abbruch);
            return EmailWechselErgebnis.Bestaetigt;
        }

        public async Task LinksAnfordernAsync(string? email, string basisUrl, CancellationToken abbruch = default)
        {
            if (!EmailAdresse.IstGueltig(email))
                return;
            var adresse = EmailAdresse.Normalisieren(email!);
            var heute = DateOnly.FromDateTime(Ortszeit.Jetzt(_zeit));

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var anmeldungen = await kontext.Anmeldungen
                .Include(a => a.Veranstaltung!).ThenInclude(v => v.Tage)
                .Where(a => a.Email == adresse
                            && (a.Status == AnmeldungStatus.Unbestaetigt || a.Status == AnmeldungStatus.Angemeldet || a.Status == AnmeldungStatus.Warteliste)
                            && a.Veranstaltung!.Status == VeranstaltungStatus.Veroeffentlicht
                            && a.Veranstaltung.Tage.Any(t => !t.Abgesagt && t.Datum >= heute))
                .ToListAsync(abbruch);
            if (anmeldungen.Count == 0)
                return;

            var jetzt = JetztUtc;
            var links = new List<VeranstaltungMailVorlagen.AngeforderterLink>();
            foreach (var a in anmeldungen.OrderBy(x => x.Veranstaltung!.Tage.Where(t => !t.Abgesagt).Min(t => t.Datum)))
            {
                var v = a.Veranstaltung!;
                var neu = AnmeldeToken.Erzeugen();
                AnmeldungDaten.TokenSetzen(a, neu, jetzt);
                AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.LinkVersendet, EreignisAkteur.Teilnehmer, jetzt);
                var unbestaetigt = a.Status == AnmeldungStatus.Unbestaetigt;
                links.Add(new VeranstaltungMailVorlagen.AngeforderterLink(
                    v.Titel,
                    VeranstaltungMailVorlagen.Zeitraum(v.Tage.ToList()),
                    unbestaetigt ? VeranstaltungLinks.BestaetigenUrl(basisUrl, neu.Klartext) : VeranstaltungLinks.MeineAnmeldungUrl(basisUrl, neu.Klartext),
                    unbestaetigt));
            }

            _mails.Allgemein(kontext, adresse, VeranstaltungMailVorlagen.LinksAngefordert(links));
            try
            {
                await kontext.SaveChangesAsync(abbruch);
                _mails.VersandAnstossen();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Eine der Anmeldungen wurde gleichzeitig geändert: dann gelten die alten Links weiter, es geht keine Mail raus
            }
        }

        public async Task<string?> InfoAbmeldungPruefenAsync(string token, CancellationToken abbruch = default)
        {
            if (AnmeldeToken.Hash(token) is not { } hash)
                return null;

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            return await kontext.AnmeldungInfoEmails.AsNoTracking()
                .Where(i => i.AbmeldeTokenHash == hash && i.AbgemeldetUtc == null)
                .Select(i => i.Anmeldung!.Veranstaltung!.Titel)
                .SingleOrDefaultAsync(abbruch);
        }

        public async Task<bool> InfoAbmeldenAsync(string token, CancellationToken abbruch = default)
        {
            if (AnmeldeToken.Hash(token) is not { } hash)
                return false;

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var info = await kontext.AnmeldungInfoEmails
                .Include(i => i.Anmeldung)
                .SingleOrDefaultAsync(i => i.AbmeldeTokenHash == hash, abbruch);
            if (info is null)
                return false;
            if (info.AbgemeldetUtc is not null)
                return true;

            var jetzt = JetztUtc;
            info.AbgemeldetUtc = jetzt;
            // Die Info-Adresse handelt selbst, nicht der Anmelder
            AnmeldungDaten.EreignisHinzufuegen(info.Anmeldung!, AnmeldungEreignisArt.InfoEmailsGeaendert, EreignisAkteur.System, jetzt, EreignisDiff.Wert(info.Email, null));
            await kontext.SaveChangesAsync(abbruch);
            return true;
        }

        // ---------------------------------------------------------------------------------------------

        /// <summary>Offene Transaktion mit Sperre der Veranstaltung und den geladenen Daten. Ohne Commit wird beim Dispose zurückgerollt.</summary>
        private readonly record struct Sitzung(
            Veranstaltung Veranstaltung, List<VeranstaltungsTag> Tage, List<Anmeldung> Anmeldungen, Anmeldung Anmeldung, IDbContextTransaction Transaktion)
            : IAsyncDisposable
        {
            public void Deconstruct(out Veranstaltung v, out List<VeranstaltungsTag> tage, out List<Anmeldung> anmeldungen, out Anmeldung anmeldung) =>
                (v, tage, anmeldungen, anmeldung) = (Veranstaltung, Tage, Anmeldungen, Anmeldung);

            public ValueTask DisposeAsync() => Transaktion.DisposeAsync();
        }

        private enum TokenArt
        {
            /// <summary>Verwaltungslink (Anmeldung.TokenHash).</summary>
            Verwaltung,
            /// <summary>Link zum Bestätigen einer neuen Adresse (Anmeldung.NeueEmailTokenHash).</summary>
            EmailWechsel
        }

        private static byte[]? TokenHash(Anmeldung a, TokenArt art) => art == TokenArt.Verwaltung ? a.TokenHash : a.NeueEmailTokenHash;

        /// <summary>
        /// Sucht die Anmeldung über den Hash eines Tokens, sperrt die Veranstaltung und lädt unter der Sperre
        /// alles neu (der Link könnte inzwischen ungültig geworden sein). null, wenn der Link nicht (mehr) passt.
        /// </summary>
        private static async Task<Sitzung?> GesperrtLadenAsync(ApplicationDbContext kontext, TokenArt art, string token, CancellationToken abbruch)
        {
            if (AnmeldeToken.Hash(token) is not { } hash)
                return null;

            var suche = art == TokenArt.Verwaltung
                ? kontext.Anmeldungen.Where(a => a.TokenHash == hash)
                : kontext.Anmeldungen.Where(a => a.NeueEmailTokenHash == hash);
            var veranstaltungId = await suche.Select(a => (int?)a.VeranstaltungId).SingleOrDefaultAsync(abbruch);
            if (veranstaltungId is null)
                return null;

            var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            try
            {
                await VeranstaltungSperre.SetzenAsync(kontext, veranstaltungId.Value, abbruch);
                var v = await kontext.Veranstaltungen.Include(x => x.Tage).SingleAsync(x => x.Id == veranstaltungId, abbruch);
                var anmeldungen = await kontext.Anmeldungen
                    .Include(a => a.Tage)
                    .Include(a => a.InfoEmails)
                    .Where(a => a.VeranstaltungId == v.Id)
                    .ToListAsync(abbruch);
                var anmeldung = anmeldungen.SingleOrDefault(a => TokenHash(a, art) is { } wert && wert.SequenceEqual(hash));
                if (anmeldung is null)
                {
                    await transaktion.DisposeAsync();
                    return null;
                }
                return new Sitzung(v, v.Tage.ToList(), anmeldungen, anmeldung, transaktion);
            }
            catch
            {
                await transaktion.DisposeAsync();
                throw;
            }
        }

        private async Task AbschliessenAsync(ApplicationDbContext kontext, Sitzung? sitzung, CancellationToken abbruch)
        {
            await kontext.SaveChangesAsync(abbruch);
            await sitzung!.Value.Transaktion.CommitAsync(abbruch);
            _mails.VersandAnstossen();
        }
    }
}
