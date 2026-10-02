using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <param name="Max">null = unbegrenzt.</param>
    public sealed record TagBelegung(int Id, DateOnly Datum, TimeOnly Beginn, TimeOnly? Ende, string? Titel, bool Abgesagt, int? Max, int Belegt) : ITermin
    {
        /// <summary>Für die Formularfelder (AnmeldeFelder); FreiePlaetze null = unbegrenzt.</summary>
        public TagAnzeige AlsAnzeige() => new(Id, Datum, Beginn, Ende, Titel, Abgesagt, Max is { } max ? Math.Max(0, max - Belegt) : null);
    }

    public sealed record TeilnehmerZeile(
        int Id,
        string Vorname,
        string Nachname,
        string Email,
        AnmeldungStatus Status,
        int AnzahlBegleitpersonen,
        IReadOnlyList<int> TagIds,
        string? Verein,
        string? Graduierung,
        AnmeldungQuelle Quelle,
        DateTime ErstelltUtc,
        bool HatUngeseheneAenderungen,
        bool HatNotiz);

    public sealed record EreignisAnzeige(int AnmeldungId, string Teilnehmer, DateTime ZeitpunktUtc, EreignisAkteur Akteur, string? AkteurName, AnmeldungEreignisArt Art, string Text, bool Ungesehen);

    public sealed record VeranstaltungUebersicht(
        int Id,
        string Titel,
        string Slug,
        VeranstaltungStatus Status,
        VeranstaltungSichtbarkeit Sichtbarkeit,
        Teilnahmemodus Teilnahmemodus,
        string? Abteilung,
        string? Ort,
        AnmeldeFormularEinstellungen Formular,
        IReadOnlyList<TagBelegung> Tage,
        IReadOnlyList<TeilnehmerZeile> Teilnehmer,
        IReadOnlyList<EreignisAnzeige> LetzteAenderungen)
    {
        public int AktiveAnmeldungen => Teilnehmer.Count(t => AnmeldungStatusUebergaenge.IstAktiv(t.Status));
        public int BestaetigtePersonen => Teilnehmer.Where(t => t.Status == AnmeldungStatus.Angemeldet).Sum(t => 1 + t.AnzahlBegleitpersonen);
        public int Unbestaetigt => Teilnehmer.Count(t => t.Status == AnmeldungStatus.Unbestaetigt);
        public int UngeseheneAenderungen => Teilnehmer.Count(t => t.HatUngeseheneAenderungen);
    }

    public sealed record AnmeldungDetail(
        int Id,
        AnmeldungStatus Status,
        string? StatusGrund,
        AnmeldungQuelle Quelle,
        string? Telefon,
        string? Bemerkung,
        string? AdminNotiz,
        string? NeueEmailAusstehend,
        IReadOnlyList<string> InfoEmails,
        IReadOnlyList<string> AbgemeldeteInfoEmails,
        DateTime ErstelltUtc,
        DateTime? EmailBestaetigtUtc,
        AnmeldeEingabe Daten,
        IReadOnlyList<EreignisAnzeige> Historie);

    public interface ITeilnehmerVerwaltungService
    {
        /// <summary>null, wenn es die Veranstaltung nicht gibt oder der Benutzer sie nicht verwalten darf.</summary>
        Task<VeranstaltungUebersicht?> UebersichtAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<AnmeldungDetail?> DetailAsync(int veranstaltungId, int anmeldungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Ohne anmeldungIds: alle Anmeldungen der Veranstaltung.</summary>
        Task<VerwaltungsErgebnis> AlsGesehenMarkierenAsync(int veranstaltungId, IReadOnlyCollection<int>? anmeldungIds, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> AblehnenAsync(int veranstaltungId, int anmeldungId, string? grund, bool benachrichtigen, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Abgelehnt → Angemeldet, wenn Platz ist; der Teilnehmer bekommt eine Bestätigung mit neuem Verwaltungslink.</summary>
        Task<VerwaltungsErgebnis> AblehnungZuruecknehmenAsync(int veranstaltungId, int anmeldungId, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Anmeldung durch den Organisator (z. B. telefonisch), sofort bestätigt.</summary>
        Task<VerwaltungsErgebnis> ManuellAnmeldenAsync(int veranstaltungId, AnmeldeEingabe eingabe, bool benachrichtigen, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Daten korrigieren; anders als im Self-Service gilt eine neue Adresse sofort und es gibt keine Frist.</summary>
        Task<VerwaltungsErgebnis> BearbeitenAsync(int veranstaltungId, int anmeldungId, AnmeldeEingabe eingabe, bool benachrichtigen, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Interne Notiz, für Teilnehmer nicht sichtbar; erzeugt kein Ereignis.</summary>
        Task<VerwaltungsErgebnis> NotizSpeichernAsync(int veranstaltungId, int anmeldungId, string? notiz, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> LinkErneutSendenAsync(int veranstaltungId, int anmeldungId, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>CSV der aktiven Anmeldungen; null ohne Berechtigung.</summary>
        Task<(string Dateiname, byte[] Inhalt)?> CsvExportAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);
    }

    /// <summary>
    /// Was Organisatoren auf der Übersichtsseite mit Anmeldungen tun. Rechte wie in der Verwaltung (VeranstaltungRechte);
    /// Änderungen werden als Ereignis mit Akteur "Organisator" protokolliert. Platzrelevante Aktionen laufen unter der Sperre.
    /// </summary>
    public sealed class TeilnehmerVerwaltungService : ITeilnehmerVerwaltungService
    {
        private const string KeineBerechtigung = "Du darfst diese Veranstaltung nicht verwalten.";
        private const string NichtGefunden = "Die Anmeldung wurde nicht gefunden.";
        private const int AnzahlLetzteAenderungen = 30;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly AnmeldungMailVersand _mails;
        private readonly TimeProvider _zeit;
        private readonly VeranstaltungenOptionen _optionen;

        public TeilnehmerVerwaltungService(
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

        public async Task<VeranstaltungUebersicht?> UebersichtAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var v = await kontext.Veranstaltungen.AsNoTracking()
                .Include(x => x.Abteilung)
                .Include(x => x.Tage)
                .SingleOrDefaultAsync(x => x.Id == veranstaltungId, abbruch);
            if (v is null || !VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.Abteilung?.Name))
                return null;

            var anmeldungen = await kontext.Anmeldungen.AsNoTracking()
                .Include(a => a.Tage)
                .Include(a => a.Ereignisse)
                .Where(a => a.VeranstaltungId == v.Id)
                .AsSplitQuery()
                .ToListAsync(abbruch);
            var namen = await AkteurNamenAsync(kontext, anmeldungen.SelectMany(a => a.Ereignisse), abbruch);

            var tage = TerminText.Sortiert(v.Tage).ToList();
            var belegt = KapazitaetsRechner.BelegungProTag(
                v.Teilnahmemodus, tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList(),
                anmeldungen.Select(AnmeldungDaten.Belegung), JetztUtc);

            var letzteAenderungen = anmeldungen
                .SelectMany(a => a.Ereignisse.Select(e => (Ereignis: e, Anzeige: Anzeige(a, e, namen))))
                // Bei gleichem Zeitpunkt entscheidet die Reihenfolge des Speicherns (Id)
                .OrderByDescending(x => x.Ereignis.ZeitpunktUtc)
                .ThenByDescending(x => x.Ereignis.Id)
                .Take(AnzahlLetzteAenderungen)
                .Select(x => x.Anzeige)
                .ToList();

            return new VeranstaltungUebersicht(
                v.Id,
                v.Titel,
                v.Slug,
                v.Status,
                v.Sichtbarkeit,
                v.Teilnahmemodus,
                v.Abteilung?.Name,
                v.Ort,
                new AnmeldeFormularEinstellungen(v.Teilnahmemodus, v.MinTageBeiTeilanmeldung, v.MaxBegleitpersonen, v.TelefonFeld, v.VereinFeld, v.GraduierungFeld, v.BemerkungFeld),
                tage.Select(t => new TagBelegung(t.Id, t.Datum, t.Beginn, t.Ende, t.Titel, t.Abgesagt, t.MaxTeilnehmer, belegt.GetValueOrDefault(t.Id))).ToList(),
                anmeldungen
                    .OrderBy(a => a.Nachname).ThenBy(a => a.Vorname)
                    .Select(a => new TeilnehmerZeile(
                        a.Id, a.Vorname, a.Nachname, a.Email, a.Status, a.AnzahlBegleitpersonen,
                        a.Tage.Select(t => t.VeranstaltungsTagId).ToList(), a.Verein, a.Graduierung, a.Quelle, a.ErstelltUtc,
                        IstUngesehen(a, a.Ereignisse), !string.IsNullOrWhiteSpace(a.AdminNotiz)))
                    .ToList(),
                letzteAenderungen);
        }

        public async Task<AnmeldungDetail?> DetailAsync(int veranstaltungId, int anmeldungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (await PruefeRechteAsync(kontext, veranstaltungId, benutzer, abbruch) is not null)
                return null;

            var a = await kontext.Anmeldungen.AsNoTracking()
                .Include(x => x.Tage)
                .Include(x => x.InfoEmails)
                .Include(x => x.Ereignisse)
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == anmeldungId && x.VeranstaltungId == veranstaltungId, abbruch);
            if (a is null)
                return null;

            var namen = await AkteurNamenAsync(kontext, a.Ereignisse, abbruch);
            return new AnmeldungDetail(
                a.Id,
                a.Status,
                a.StatusGrund,
                a.Quelle,
                a.Telefon,
                a.Bemerkung,
                a.AdminNotiz,
                a.NeueEmail,
                a.InfoEmails.Where(i => i.AbgemeldetUtc is null).Select(i => i.Email).Order().ToList(),
                a.InfoEmails.Where(i => i.AbgemeldetUtc is not null).Select(i => i.Email).Order().ToList(),
                a.ErstelltUtc,
                a.EmailBestaetigtUtc,
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
                // Bei gleichem Zeitpunkt entscheidet die Reihenfolge des Speicherns (Id)
                a.Ereignisse.OrderByDescending(e => e.ZeitpunktUtc).ThenByDescending(e => e.Id).Select(e => Anzeige(a, e, namen)).ToList());
        }

        public async Task<VerwaltungsErgebnis> AlsGesehenMarkierenAsync(int veranstaltungId, IReadOnlyCollection<int>? anmeldungIds, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (await PruefeRechteAsync(kontext, veranstaltungId, benutzer, abbruch) is { } fehler)
                return VerwaltungsErgebnis.MitFehler(fehler);

            var jetzt = JetztUtc;
            var abfrage = kontext.Anmeldungen.Where(a => a.VeranstaltungId == veranstaltungId);
            if (anmeldungIds is not null)
                abfrage = abfrage.Where(a => anmeldungIds.Contains(a.Id));
            await abfrage.ExecuteUpdateAsync(s => s.SetProperty(a => a.AdminGesehenUtc, jetzt), abbruch);
            return VerwaltungsErgebnis.Ok(veranstaltungId);
        }

        public async Task<VerwaltungsErgebnis> AblehnenAsync(int veranstaltungId, int anmeldungId, string? grund, bool benachrichtigen, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (sitzung, fehler) = await GesperrtLadenAsync(kontext, veranstaltungId, anmeldungId, benutzer, abbruch);
            if (sitzung is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);
            await using var sperre = sitzung;
            var (v, _, _, a) = sitzung;

            if (!AnmeldungStatusUebergaenge.IstErlaubt(a.Status, AnmeldungStatus.Abgelehnt, EreignisAkteur.Admin))
                return VerwaltungsErgebnis.MitFehler($"Eine Anmeldung mit Status \"{a.Status.Beschreibung()}\" kann nicht abgelehnt werden.");

            var jetzt = JetztUtc;
            var bereinigt = string.IsNullOrWhiteSpace(grund) ? null : grund.Trim();
            AnmeldungDaten.StatusSetzen(a, AnmeldungStatus.Abgelehnt, EreignisAkteur.Admin, bereinigt);
            a.ReserviertBisUtc = null;
            a.GeaendertUtc = jetzt;
            AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.Abgelehnt, EreignisAkteur.Admin, jetzt, EreignisDiff.Grund(bereinigt), benutzer.UserId);
            if (benachrichtigen)
                _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.AbgelehntDurchOrganisator(v, a, bereinigt));

            return await AbschliessenAsync(kontext, sitzung, a.Id, abbruch);
        }

        public async Task<VerwaltungsErgebnis> AblehnungZuruecknehmenAsync(int veranstaltungId, int anmeldungId, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (sitzung, fehler) = await GesperrtLadenAsync(kontext, veranstaltungId, anmeldungId, benutzer, abbruch);
            if (sitzung is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);
            await using var sperre = sitzung;
            var (v, tage, anmeldungen, a) = sitzung;

            if (a.Status != AnmeldungStatus.Abgelehnt)
                return VerwaltungsErgebnis.MitFehler("Die Anmeldung ist nicht abgelehnt.");
            if (PlatzFehler(v, tage, anmeldungen, a, a.Tage.Select(t => t.VeranstaltungsTagId).ToList(), 1 + a.AnzahlBegleitpersonen) is { } platz)
                return VerwaltungsErgebnis.MitFehler(platz);

            var jetzt = JetztUtc;
            AnmeldungDaten.StatusSetzen(a, AnmeldungStatus.Angemeldet, EreignisAkteur.Admin);
            a.GeaendertUtc = jetzt;
            var link = AnmeldeToken.Erzeugen();
            AnmeldungDaten.TokenSetzen(a, link, jetzt);
            AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.AblehnungZurueckgenommen, EreignisAkteur.Admin, jetzt, akteurUserId: benutzer.UserId);
            _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.Bestaetigung(v, tage, a, VeranstaltungLinks.MeineAnmeldungUrl(basisUrl, link.Klartext)));

            return await AbschliessenAsync(kontext, sitzung, a.Id, abbruch);
        }

        public async Task<VerwaltungsErgebnis> ManuellAnmeldenAsync(int veranstaltungId, AnmeldeEingabe eingabe, bool benachrichtigen, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (await PruefeRechteAsync(kontext, veranstaltungId, benutzer, abbruch) is { } rechte)
                return VerwaltungsErgebnis.MitFehler(rechte);

            await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            await VeranstaltungSperre.SetzenAsync(kontext, veranstaltungId, abbruch);
            var v = await kontext.Veranstaltungen.Include(x => x.Tage).SingleAsync(x => x.Id == veranstaltungId, abbruch);
            var tage = v.Tage.ToList();
            var anmeldungen = await AnmeldungenLadenAsync(kontext, v.Id, abbruch);

            // Die Einwilligung holt der Organisator mündlich ein (z. B. am Telefon)
            eingabe.DatenschutzAkzeptiert = true;
            var pruefung = AnmeldeValidierung.Pruefen(v, tage, eingabe, _optionen.MaxInfoEmails);
            if (!pruefung.IstGueltig)
                return VerwaltungsErgebnis.MitFehlern(pruefung.Fehler.Values.ToList());
            var daten = pruefung.Anmeldung!;

            var vorhanden = anmeldungen.SingleOrDefault(a => a.Email == daten.Email);
            if (vorhanden is not null && vorhanden.Status != AnmeldungStatus.Storniert)
                return VerwaltungsErgebnis.MitFehler($"Mit dieser Adresse gibt es bereits eine Anmeldung (Status \"{vorhanden.Status.Beschreibung()}\").");
            var anmeldung = vorhanden ?? new Anmeldung { VeranstaltungId = v.Id, Email = daten.Email, ErstelltUtc = JetztUtc };
            if (PlatzFehler(v, tage, anmeldungen, anmeldung, daten.TagIds, 1 + daten.AnzahlBegleitpersonen) is { } platz)
                return VerwaltungsErgebnis.MitFehler(platz);

            var jetzt = JetztUtc;
            AnmeldungDaten.Uebernehmen(anmeldung, daten, jetzt);
            anmeldung.Quelle = AnmeldungQuelle.Admin;
            anmeldung.DatenschutzAkzeptiertUtc = jetzt;
            anmeldung.ReserviertBisUtc = null;
            AnmeldungDaten.StatusSetzen(anmeldung, AnmeldungStatus.Angemeldet, EreignisAkteur.Admin);
            var link = AnmeldeToken.Erzeugen();
            AnmeldungDaten.TokenSetzen(anmeldung, link, jetzt);
            AnmeldungDaten.EreignisHinzufuegen(anmeldung, vorhanden is null ? AnmeldungEreignisArt.Angelegt : AnmeldungEreignisArt.Reaktiviert,
                EreignisAkteur.Admin, jetzt, akteurUserId: benutzer.UserId);
            if (vorhanden is null)
                kontext.Anmeldungen.Add(anmeldung);
            await kontext.SaveChangesAsync(abbruch);

            if (benachrichtigen)
            {
                _mails.AnTeilnehmer(kontext, v, anmeldung, VeranstaltungMailVorlagen.Bestaetigung(v, tage, anmeldung, VeranstaltungLinks.MeineAnmeldungUrl(basisUrl, link.Klartext)));
                _mails.InfoMails(kontext, v, tage, anmeldung, basisUrl);
            }
            await kontext.SaveChangesAsync(abbruch);
            await transaktion.CommitAsync(abbruch);
            _mails.VersandAnstossen();
            return VerwaltungsErgebnis.Ok(anmeldung.Id);
        }

        public async Task<VerwaltungsErgebnis> BearbeitenAsync(int veranstaltungId, int anmeldungId, AnmeldeEingabe eingabe, bool benachrichtigen, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (sitzung, fehler) = await GesperrtLadenAsync(kontext, veranstaltungId, anmeldungId, benutzer, abbruch);
            if (sitzung is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);
            await using var sperre = sitzung;
            var (v, tage, anmeldungen, a) = sitzung;

            eingabe.DatenschutzAkzeptiert = true;
            var pruefung = AnmeldeValidierung.Pruefen(v, tage, eingabe, _optionen.MaxInfoEmails);
            if (!pruefung.IstGueltig)
                return VerwaltungsErgebnis.MitFehlern(pruefung.Fehler.Values.ToList());
            var daten = pruefung.Anmeldung!;

            if (daten.Email != a.Email && anmeldungen.Any(x => x.Id != a.Id && x.Email == daten.Email))
                return VerwaltungsErgebnis.MitFehler("Mit dieser Adresse gibt es für diese Veranstaltung bereits eine Anmeldung.");
            if (AnmeldungStatusUebergaenge.IstAktiv(a.Status)
                && PlatzFehler(v, tage, anmeldungen, a, daten.TagIds, 1 + daten.AnzahlBegleitpersonen) is { } platz)
                return VerwaltungsErgebnis.MitFehler(platz);

            var jetzt = JetztUtc;
            var alt = AnmeldungStand.Von(a, tage);
            var alteEmail = a.Email;
            var alteInfoEmails = a.InfoEmails.Select(i => i.Email).ToHashSet();
            AnmeldungDaten.Uebernehmen(a, daten, jetzt);
            var aenderungen = EreignisDiff.Erstellen(alt, AnmeldungStand.Von(a, tage)).ToList();
            if (daten.Email != alteEmail)
            {
                // Der Organisator korrigiert z. B. einen Tippfehler: gilt sofort, ein offener Wechsel des Teilnehmers verfällt
                a.Email = daten.Email;
                a.NeueEmail = null;
                a.NeueEmailTokenHash = null;
                aenderungen.Add(new EreignisAenderung(AnmeldungEreignisArt.EmailGeaendert, EreignisDiff.Wert(alteEmail, daten.Email)));
            }
            if (aenderungen.Count == 0)
                return VerwaltungsErgebnis.Ok(a.Id);

            foreach (var aenderung in aenderungen)
                AnmeldungDaten.EreignisHinzufuegen(a, aenderung.Art, EreignisAkteur.Admin, jetzt, aenderung.DetailsJson, benutzer.UserId);

            if (benachrichtigen && a.Status == AnmeldungStatus.Angemeldet)
            {
                _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.AenderungGespeichert(v, tage, a), EmailPrioritaet.Normal);
                var neue = a.InfoEmails.Select(i => i.Email).Where(e => !alteInfoEmails.Contains(e)).ToList();
                if (neue.Count > 0)
                    _mails.InfoMails(kontext, v, tage, a, basisUrl, neue);
            }

            return await AbschliessenAsync(kontext, sitzung, a.Id, abbruch);
        }

        public async Task<VerwaltungsErgebnis> NotizSpeichernAsync(int veranstaltungId, int anmeldungId, string? notiz, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (await PruefeRechteAsync(kontext, veranstaltungId, benutzer, abbruch) is { } fehler)
                return VerwaltungsErgebnis.MitFehler(fehler);

            var bereinigt = string.IsNullOrWhiteSpace(notiz) ? null : notiz.Trim();
            if (bereinigt?.Length > 2000)
                return VerwaltungsErgebnis.MitFehler("Die Notiz darf höchstens 2000 Zeichen lang sein.");

            var geaendert = await kontext.Anmeldungen
                .Where(a => a.Id == anmeldungId && a.VeranstaltungId == veranstaltungId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.AdminNotiz, bereinigt), abbruch);
            return geaendert == 1 ? VerwaltungsErgebnis.Ok(anmeldungId) : VerwaltungsErgebnis.MitFehler(NichtGefunden);
        }

        public async Task<VerwaltungsErgebnis> LinkErneutSendenAsync(int veranstaltungId, int anmeldungId, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (sitzung, fehler) = await GesperrtLadenAsync(kontext, veranstaltungId, anmeldungId, benutzer, abbruch);
            if (sitzung is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);
            await using var sperre = sitzung;
            var (v, _, _, a) = sitzung;

            if (a.Status is not (AnmeldungStatus.Angemeldet or AnmeldungStatus.Warteliste or AnmeldungStatus.Unbestaetigt))
                return VerwaltungsErgebnis.MitFehler("Für diese Anmeldung gibt es keinen gültigen Link.");

            var jetzt = JetztUtc;
            var link = AnmeldeToken.Erzeugen();
            AnmeldungDaten.TokenSetzen(a, link, jetzt);
            AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.LinkVersendet, EreignisAkteur.Admin, jetzt, akteurUserId: benutzer.UserId);
            var unbestaetigt = a.Status == AnmeldungStatus.Unbestaetigt;
            var url = unbestaetigt ? VeranstaltungLinks.BestaetigenUrl(basisUrl, link.Klartext) : VeranstaltungLinks.MeineAnmeldungUrl(basisUrl, link.Klartext);
            _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.LinkErneut(v, a, url, unbestaetigt));

            return await AbschliessenAsync(kontext, sitzung, a.Id, abbruch);
        }

        public async Task<(string Dateiname, byte[] Inhalt)?> CsvExportAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var v = await kontext.Veranstaltungen.AsNoTracking()
                .Include(x => x.Abteilung)
                .Include(x => x.Tage)
                .SingleOrDefaultAsync(x => x.Id == veranstaltungId, abbruch);
            if (v is null || !VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.Abteilung?.Name))
                return null;

            var aktive = await kontext.Anmeldungen.AsNoTracking()
                .Include(a => a.Tage)
                .Where(a => a.VeranstaltungId == v.Id
                            && (a.Status == AnmeldungStatus.Angemeldet || a.Status == AnmeldungStatus.Warteliste || a.Status == AnmeldungStatus.Unbestaetigt))
                .ToListAsync(abbruch);

            var datum = DateOnly.FromDateTime(Ortszeit.Jetzt(_zeit));
            return ($"teilnehmer-{v.Slug}-{datum:yyyy-MM-dd}.csv", TeilnehmerCsv.Erstellen(aktive, v.Tage.ToList(), v.Teilnahmemodus));
        }

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// "Neu" sind Änderungen von Teilnehmern bzw. dem System nach dem letzten "als gesehen markieren".
        /// Aktionen der Organisatoren selbst gelten nie als neu; so verdecken sie auch keine ungesehenen Änderungen.
        /// </summary>
        private static bool IstNeu(Anmeldung a, AnmeldungEreignis e) =>
            e.Akteur != EreignisAkteur.Admin && (a.AdminGesehenUtc is null || e.ZeitpunktUtc > a.AdminGesehenUtc);

        private static bool IstUngesehen(Anmeldung a, IEnumerable<AnmeldungEreignis> ereignisse) =>
            ereignisse.Any(e => IstNeu(a, e));

        private static EreignisAnzeige Anzeige(Anmeldung a, AnmeldungEreignis e, IReadOnlyDictionary<string, string> namen) => new(
            a.Id,
            $"{a.Vorname} {a.Nachname}",
            e.ZeitpunktUtc,
            e.Akteur,
            e.AkteurUserId is { } id && namen.TryGetValue(id, out var name) ? name : null,
            e.Art,
            EreignisText.Beschreiben(e.Art, e.DetailsJson),
            IstNeu(a, e));

        private static async Task<IReadOnlyDictionary<string, string>> AkteurNamenAsync(ApplicationDbContext kontext, IEnumerable<AnmeldungEreignis> ereignisse, CancellationToken abbruch)
        {
            var ids = ereignisse.Select(e => e.AkteurUserId).OfType<string>().Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<string, string>();
            var benutzer = await kontext.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Vorname, u.Name, u.UserName })
                .ToListAsync(abbruch);
            return benutzer.ToDictionary(u => u.Id, u => $"{u.Vorname} {u.Name}".Trim() is { Length: > 0 } n ? n : u.UserName ?? u.Id);
        }

        /// <summary>null, wenn der Benutzer die Veranstaltung verwalten darf; sonst die Fehlermeldung.</summary>
        private static async Task<string?> PruefeRechteAsync(ApplicationDbContext kontext, int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch)
        {
            var v = await kontext.Veranstaltungen.AsNoTracking()
                .Where(x => x.Id == veranstaltungId)
                .Select(x => new { x.AbteilungId, AbteilungName = x.Abteilung != null ? x.Abteilung.Name : null })
                .SingleOrDefaultAsync(abbruch);
            return v is null ? "Die Veranstaltung wurde nicht gefunden."
                : VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.AbteilungName) ? null
                : KeineBerechtigung;
        }

        private static Task<List<Anmeldung>> AnmeldungenLadenAsync(ApplicationDbContext kontext, int veranstaltungId, CancellationToken abbruch) =>
            kontext.Anmeldungen
                .Include(a => a.Tage)
                .Include(a => a.InfoEmails)
                .Where(a => a.VeranstaltungId == veranstaltungId)
                .AsSplitQuery()
                .ToListAsync(abbruch);

        /// <summary>null, wenn die Anmeldung mit dieser Personenzahl an ihren Tagen Platz hat (ohne sich selbst mitzuzählen).</summary>
        private string? PlatzFehler(Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, IReadOnlyCollection<Anmeldung> anmeldungen, Anmeldung anmeldung, IReadOnlyCollection<int> tagIds, int personen)
        {
            var pruefung = KapazitaetsRechner.Pruefen(
                v.Teilnahmemodus,
                tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList(),
                anmeldungen.Select(AnmeldungDaten.Belegung).ToList(),
                JetztUtc,
                tagIds,
                personen,
                anmeldung.Id == 0 ? null : anmeldung.Id);
            return pruefung.Passt ? null : "Dafür ist kein Platz mehr frei.";
        }

        private sealed class Sitzung(Veranstaltung veranstaltung, List<VeranstaltungsTag> tage, List<Anmeldung> anmeldungen, Anmeldung anmeldung, IDbContextTransaction transaktion)
            : IAsyncDisposable
        {
            public IDbContextTransaction Transaktion { get; } = transaktion;

            public void Deconstruct(out Veranstaltung v, out List<VeranstaltungsTag> t, out List<Anmeldung> alle, out Anmeldung a) =>
                (v, t, alle, a) = (veranstaltung, tage, anmeldungen, anmeldung);

            public ValueTask DisposeAsync() => Transaktion.DisposeAsync();
        }

        /// <summary>Rechte prüfen, Veranstaltung sperren und Anmeldung samt aller Anmeldungen (für die Platzprüfung) laden.</summary>
        private async Task<(Sitzung? Sitzung, string? Fehler)> GesperrtLadenAsync(
            ApplicationDbContext kontext, int veranstaltungId, int anmeldungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch)
        {
            if (await PruefeRechteAsync(kontext, veranstaltungId, benutzer, abbruch) is { } fehler)
                return (null, fehler);

            var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            try
            {
                await VeranstaltungSperre.SetzenAsync(kontext, veranstaltungId, abbruch);
                var v = await kontext.Veranstaltungen.Include(x => x.Tage).SingleAsync(x => x.Id == veranstaltungId, abbruch);
                var anmeldungen = await AnmeldungenLadenAsync(kontext, v.Id, abbruch);
                var anmeldung = anmeldungen.SingleOrDefault(a => a.Id == anmeldungId);
                if (anmeldung is null)
                {
                    await transaktion.DisposeAsync();
                    return (null, NichtGefunden);
                }
                return (new Sitzung(v, v.Tage.ToList(), anmeldungen, anmeldung, transaktion), null);
            }
            catch
            {
                await transaktion.DisposeAsync();
                throw;
            }
        }

        private async Task<VerwaltungsErgebnis> AbschliessenAsync(ApplicationDbContext kontext, Sitzung sitzung, int anmeldungId, CancellationToken abbruch)
        {
            await kontext.SaveChangesAsync(abbruch);
            await sitzung.Transaktion.CommitAsync(abbruch);
            _mails.VersandAnstossen();
            return VerwaltungsErgebnis.Ok(anmeldungId);
        }
    }
}
