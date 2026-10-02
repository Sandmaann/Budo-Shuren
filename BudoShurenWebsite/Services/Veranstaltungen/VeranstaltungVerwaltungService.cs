using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    public sealed record VeranstaltungListenEintrag(
        int Id,
        string Titel,
        string Slug,
        VeranstaltungStatus Status,
        DateOnly? ErsterTag,
        DateOnly? LetzterTag,
        string? Abteilung,
        int AktiveAnmeldungen,
        int BestaetigtePersonen,
        int UngeseheneAenderungen);

    public sealed record EmpfaengerAnzeige(
        int Id,
        string Anzeigename,
        string? Email,
        string? UserId,
        bool IstErsteller,
        BenachrichtigungEreignisse Ereignisse,
        BenachrichtigungModus Modus,
        bool Abgemeldet)
    {
        public bool IstBenutzer => UserId != null;
    }

    public sealed record BenutzerAuswahl(string UserId, string Anzeigename, string? Email);

    public sealed record AbteilungAuswahl(string Id, string Name, string? Email);

    /// <summary>Ergebnis einer Verwaltungsaktion: entweder erfolgreich (mit Id) oder eine Liste verständlicher Fehler.</summary>
    public sealed record VerwaltungsErgebnis(int? Id, IReadOnlyList<string> Fehler)
    {
        public bool Erfolgreich => Fehler.Count == 0;

        public static VerwaltungsErgebnis Ok(int id) => new(id, []);

        public static VerwaltungsErgebnis MitFehlern(IReadOnlyList<string> fehler) => new(null, fehler);

        public static VerwaltungsErgebnis MitFehler(string fehler) => new(null, [fehler]);
    }

    public interface IVeranstaltungVerwaltungService
    {
        Task<IReadOnlyList<VeranstaltungListenEintrag>> ListeAsync(VerwaltungsBenutzer benutzer, bool mitArchivierten, CancellationToken abbruch = default);

        Task<IReadOnlyList<AbteilungAuswahl>> AbteilungenAsync(VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VeranstaltungEingabe> NeueEingabeAsync(VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>null, wenn es die Veranstaltung nicht gibt oder der Benutzer sie nicht verwalten darf.</summary>
        Task<VeranstaltungEingabe?> EingabeLadenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> SpeichernAsync(VeranstaltungEingabe eingabe, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> VeroeffentlichenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> ArchivierenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> LoeschenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>
        /// Verkleinert ein Bild (BildKomprimierung) und speichert es; die Id kommt in eine Galerie (BlockEingabe.BildIds)
        /// und gilt erst mit dem Speichern der Veranstaltung. Die Größe begrenzt der Aufrufer (BildKomprimierung.MaximaleDateigroesse).
        /// </summary>
        Task<VerwaltungsErgebnis> BildHochladenAsync(Stream daten, string dateiname, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<IReadOnlyList<EmpfaengerAnzeige>> EmpfaengerAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Website-Benutzer, die als Empfänger ausgewählt werden können (Admins, Abteilungsleiter, Editoren).</summary>
        Task<IReadOnlyList<BenutzerAuswahl>> MoeglicheEmpfaengerAsync(CancellationToken abbruch = default);

        /// <summary>Genau eines von userId und email angeben.</summary>
        Task<VerwaltungsErgebnis> EmpfaengerHinzufuegenAsync(int veranstaltungId, string? userId, string? email, string? name, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> EmpfaengerAendernAsync(int veranstaltungId, int empfaengerId, BenachrichtigungEreignisse ereignisse, BenachrichtigungModus modus, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        Task<VerwaltungsErgebnis> EmpfaengerEntfernenAsync(int veranstaltungId, int empfaengerId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);
    }

    /// <summary>
    /// Anlegen, Bearbeiten, Veröffentlichen, Archivieren und Löschen von Veranstaltungen sowie die
    /// Empfänger der Organisator-Benachrichtigungen. Hält die Kalendereinträge der Tage aktuell.
    /// Jede Methode prüft die Rechte selbst (VeranstaltungRechte), die Seiten verlassen sich nicht darauf.
    /// </summary>
    public sealed class VeranstaltungVerwaltungService : IVeranstaltungVerwaltungService
    {
        private const string KeineBerechtigung = "Du darfst diese Veranstaltung nicht verwalten.";
        private const string NichtGefunden = "Die Veranstaltung wurde nicht gefunden.";

        private static readonly string[] EmpfaengerRollen = [Roles.Admin, Roles.Abteilungsleiter, Roles.Editor];

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SlugService _slugService;
        private readonly TimeProvider _zeit;
        private readonly IEmailWarteschlange _warteschlange;
        private readonly VeranstaltungenOptionen _optionen;

        public VeranstaltungVerwaltungService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            UserManager<ApplicationUser> userManager,
            SlugService slugService,
            TimeProvider zeit,
            IEmailWarteschlange warteschlange,
            IOptions<VeranstaltungenOptionen> optionen)
        {
            _dbFactory = dbFactory;
            _userManager = userManager;
            _slugService = slugService;
            _zeit = zeit;
            _warteschlange = warteschlange;
            _optionen = optionen.Value;
        }

        private DateTime JetztUtc => _zeit.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<VeranstaltungListenEintrag>> ListeAsync(VerwaltungsBenutzer benutzer, bool mitArchivierten, CancellationToken abbruch = default)
        {
            if (!VeranstaltungRechte.DarfModulNutzen(benutzer))
                return [];

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var abfrage = kontext.Veranstaltungen.AsNoTracking();

            if (!mitArchivierten)
                abfrage = abfrage.Where(v => v.Status != VeranstaltungStatus.Archiviert);

            if (!benutzer.IstAdmin)
            {
                // Ohne Abteilung im Konto darf ein Abteilungsleiter nichts verwalten (sonst träfe "AbteilungId == null" den Gesamtverein)
                if (string.IsNullOrWhiteSpace(benutzer.Abteilung))
                    return [];
                abfrage = abfrage.Where(v => v.AbteilungId == benutzer.Abteilung || v.Abteilung!.Name == benutzer.Abteilung);
            }

            var eintraege = await abfrage
                .Select(v => new VeranstaltungListenEintrag(
                    v.Id,
                    v.Titel,
                    v.Slug,
                    v.Status,
                    v.Tage.Where(t => !t.Abgesagt).Min(t => (DateOnly?)t.Datum),
                    v.Tage.Where(t => !t.Abgesagt).Max(t => (DateOnly?)t.Datum),
                    v.Abteilung != null ? v.Abteilung.Name : null,
                    v.Anmeldungen.Count(a => a.Status == AnmeldungStatus.Unbestaetigt || a.Status == AnmeldungStatus.Angemeldet || a.Status == AnmeldungStatus.Warteliste),
                    v.Anmeldungen.Where(a => a.Status == AnmeldungStatus.Angemeldet).Sum(a => 1 + a.AnzahlBegleitpersonen),
                    v.Anmeldungen.Count(a => a.Ereignisse.Any(e => e.Akteur != EreignisAkteur.Admin && (a.AdminGesehenUtc == null || e.ZeitpunktUtc > a.AdminGesehenUtc)))))
                .ToListAsync(abbruch);

            // Entwürfe ohne Termin zuerst, dann chronologisch
            return eintraege.OrderBy(e => e.ErsterTag.HasValue).ThenBy(e => e.ErsterTag).ThenBy(e => e.Titel).ToList();
        }

        public async Task<IReadOnlyList<AbteilungAuswahl>> AbteilungenAsync(VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var abteilungen = await kontext.Abteilungen.AsNoTracking().OrderBy(a => a.SortOrder).ToListAsync(abbruch);

            return abteilungen
                .Where(a => a.ID != null && VeranstaltungRechte.DarfVerwalten(benutzer, a.ID, a.Name))
                .Select(a => new AbteilungAuswahl(a.ID!, a.Name ?? a.ID!, a.Email))
                .ToList();
        }

        public async Task<VeranstaltungEingabe> NeueEingabeAsync(VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            var eingabe = new VeranstaltungEingabe { KontaktName = benutzer.Anzeigename };

            // Abteilungsleiter legen immer für die eigene Abteilung an
            if (!benutzer.IstAdmin)
                eingabe.AbteilungId = (await AbteilungenAsync(benutzer, abbruch)).FirstOrDefault()?.Id;

            return eingabe;
        }

        public async Task<VeranstaltungEingabe?> EingabeLadenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var v = await kontext.Veranstaltungen.AsNoTracking()
                .Include(x => x.Abteilung)
                .Include(x => x.Tage)
                .Include(x => x.Bloecke).ThenInclude(b => b.Bilder)
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == id, abbruch);

            if (v is null || !VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.Abteilung?.Name))
                return null;

            return new VeranstaltungEingabe
            {
                Id = v.Id,
                Titel = v.Titel,
                Slug = v.Slug,
                Kurzbeschreibung = v.Kurzbeschreibung,
                Bloecke = v.Bloecke.OrderBy(b => b.Sortierung).Select(b => new BlockEingabe
                {
                    Typ = b.Typ,
                    MarkdownInhalt = b.MarkdownInhalt,
                    BilderProReihe = b.BilderProReihe,
                    BildUnterschrift = b.BildUnterschrift,
                    BildIds = b.Bilder.OrderBy(bi => bi.Sortierung).Select(bi => bi.BildId).ToList()
                }).ToList(),
                Ort = v.Ort,
                Adresse = v.Adresse,
                KartenLink = v.KartenLink,
                AbteilungId = v.AbteilungId,
                KontaktName = v.KontaktName,
                KontaktEmail = v.KontaktEmail,
                Sichtbarkeit = v.Sichtbarkeit,
                AnmeldungAb = v.AnmeldungAb,
                AnmeldungBis = v.AnmeldungBis,
                AenderungenBis = v.AenderungenBis,
                Teilnahmemodus = v.Teilnahmemodus,
                MinTageBeiTeilanmeldung = v.MinTageBeiTeilanmeldung,
                MaxTeilnehmerVorgabe = v.MaxTeilnehmerVorgabe,
                MaxBegleitpersonen = v.MaxBegleitpersonen,
                DoubleOptIn = v.DoubleOptIn,
                TelefonFeld = v.TelefonFeld,
                VereinFeld = v.VereinFeld,
                GraduierungFeld = v.GraduierungFeld,
                BemerkungFeld = v.BemerkungFeld,
                ZusammenfassungUhrzeit = v.ZusammenfassungUhrzeit,
                RowVersion = v.RowVersion,
                Status = v.Status,
                WarVeroeffentlicht = v.ErstmalsVeroeffentlichtUtc != null,
                Tage = TerminText.Sortiert(v.Tage).Select(t => new TagEingabe
                {
                    Id = t.Id,
                    Datum = t.Datum,
                    Beginn = t.Beginn,
                    Ende = t.Ende,
                    Titel = t.Titel,
                    MaxTeilnehmer = t.MaxTeilnehmer,
                    Abgesagt = t.Abgesagt
                }).ToList()
            };
        }

        public async Task<VerwaltungsErgebnis> SpeichernAsync(VeranstaltungEingabe eingabe, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            var eingabeFehler = PruefeEingabe(eingabe);
            if (eingabeFehler.Count > 0)
                return VerwaltungsErgebnis.MitFehlern(eingabeFehler);

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);

            var abteilung = string.IsNullOrEmpty(eingabe.AbteilungId)
                ? null
                : await kontext.Abteilungen.SingleOrDefaultAsync(a => a.ID == eingabe.AbteilungId, abbruch);
            if (!string.IsNullOrEmpty(eingabe.AbteilungId) && abteilung is null)
                return VerwaltungsErgebnis.MitFehler("Die gewählte Abteilung gibt es nicht.");
            if (!VeranstaltungRechte.DarfVerwalten(benutzer, abteilung?.ID, abteilung?.Name))
                return VerwaltungsErgebnis.MitFehler("Du darfst keine Veranstaltung für diese Abteilung anlegen.");

            Veranstaltung v;
            if (eingabe.Id is { } id)
            {
                var vorhanden = await kontext.Veranstaltungen
                    .Include(x => x.Abteilung)
                    .Include(x => x.Tage)
                    .Include(x => x.Anmeldungen).ThenInclude(a => a.Tage)
                    .Include(x => x.Bloecke).ThenInclude(b => b.Bilder)
                    .AsSplitQuery()
                    .SingleOrDefaultAsync(x => x.Id == id, abbruch);
                if (vorhanden is null)
                    return VerwaltungsErgebnis.MitFehler(NichtGefunden);
                if (!VeranstaltungRechte.DarfVerwalten(benutzer, vorhanden.AbteilungId, vorhanden.Abteilung?.Name))
                    return VerwaltungsErgebnis.MitFehler(KeineBerechtigung);
                v = vorhanden;

                if (eingabe.RowVersion is not null)
                    kontext.Entry(v).Property(x => x.RowVersion).OriginalValue = eingabe.RowVersion;
            }
            else
            {
                v = new Veranstaltung { ErstelltVon = benutzer.UserId, ErstelltUtc = JetztUtc };
                kontext.Veranstaltungen.Add(v);
                // Der Ersteller bekommt automatisch die Organisator-Benachrichtigungen
                v.BenachrichtigungEmpfaenger.Add(new BenachrichtigungEmpfaenger
                {
                    UserId = benutzer.UserId,
                    HinzugefuegtVon = benutzer.UserId,
                    ErstelltUtc = JetztUtc,
                    BenachrichtigtBisUtc = JetztUtc
                });
            }

            var slug = await SlugErmittelnAsync(kontext, v, eingabe, abbruch);
            if (slug is null)
                return VerwaltungsErgebnis.MitFehler("Aus dem Titel lässt sich keine Adresse (Slug) erzeugen. Bitte einen Slug angeben.");

            var regelFehler = PruefeAenderungsRegeln(v, eingabe, slug);
            if (regelFehler.Count > 0)
                return VerwaltungsErgebnis.MitFehlern(regelFehler);

            var tagFehler = TageUebernehmen(kontext, v, eingabe.Tage);
            if (tagFehler is not null)
                return VerwaltungsErgebnis.MitFehler(tagFehler);

            var blockFehler = await BloeckeUebernehmenAsync(kontext, v, eingabe.Bloecke, abbruch);
            if (blockFehler is not null)
                return VerwaltungsErgebnis.MitFehler(blockFehler);

            FelderUebernehmen(v, eingabe, slug);
            v.Abteilung = abteilung;
            if (eingabe.Id is not null)
            {
                v.GeaendertVon = benutzer.UserId;
                v.GeaendertUtc = JetztUtc;
            }

            // Eine veröffentlichte Veranstaltung muss auch nach der Änderung vollständig bleiben
            if (v.Status == VeranstaltungStatus.Veroeffentlicht)
            {
                var pruefung = VeroeffentlichungsPruefung.PruefenOhneBeginn(v, v.Tage.ToList());
                if (pruefung.Count > 0)
                    return VerwaltungsErgebnis.MitFehlern(pruefung);
            }

            return await SpeichernMitKalenderAsync(kontext, v, abbruch, GalerieBildIds(eingabe.Bloecke));
        }

        public async Task<VerwaltungsErgebnis> VeroeffentlichenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (v, fehler) = await LadenFuerAktionAsync(kontext, id, benutzer, abbruch);
            if (v is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);

            if (v.Status != VeranstaltungStatus.Entwurf)
                return VerwaltungsErgebnis.MitFehler("Nur Entwürfe können veröffentlicht werden.");

            var pruefung = VeroeffentlichungsPruefung.Pruefen(v, v.Tage.ToList(), Ortszeit.Jetzt(_zeit));
            if (pruefung.Count > 0)
                return VerwaltungsErgebnis.MitFehlern(pruefung);

            v.Status = VeranstaltungStatus.Veroeffentlicht;
            v.ErstmalsVeroeffentlichtUtc ??= JetztUtc;
            v.GeaendertVon = benutzer.UserId;
            v.GeaendertUtc = JetztUtc;

            return await SpeichernMitKalenderAsync(kontext, v, abbruch);
        }

        public async Task<VerwaltungsErgebnis> ArchivierenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (v, fehler) = await LadenFuerAktionAsync(kontext, id, benutzer, abbruch);
            if (v is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);

            var letzterTag = v.Tage.Where(t => !t.Abgesagt).Select(t => (DateOnly?)t.Datum).Max();
            var heute = DateOnly.FromDateTime(Ortszeit.Jetzt(_zeit));
            if (v.Status == VeranstaltungStatus.Veroeffentlicht && letzterTag >= heute)
                return VerwaltungsErgebnis.MitFehler("Die Veranstaltung läuft noch oder steht bevor und kann noch nicht archiviert werden.");

            v.Status = VeranstaltungStatus.Archiviert;
            v.GeaendertVon = benutzer.UserId;
            v.GeaendertUtc = JetztUtc;

            return await SpeichernMitKalenderAsync(kontext, v, abbruch);
        }

        public async Task<VerwaltungsErgebnis> LoeschenAsync(int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (v, fehler) = await LadenFuerAktionAsync(kontext, id, benutzer, abbruch);
            if (v is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);

            if (v.Status != VeranstaltungStatus.Entwurf || v.ErstmalsVeroeffentlichtUtc != null)
                return VerwaltungsErgebnis.MitFehler("Nur nie veröffentlichte Entwürfe können gelöscht werden. Veröffentlichte Veranstaltungen bitte archivieren.");
            if (await kontext.Anmeldungen.AnyAsync(a => a.VeranstaltungId == id, abbruch))
                return VerwaltungsErgebnis.MitFehler("Die Veranstaltung hat Anmeldungen und kann nicht gelöscht werden.");

            // Die Bilder gehören nur zu dieser Veranstaltung (BloeckeUebernehmenAsync); ihre Daten mit löschen.
            // Bausteine laden: Remove löscht sie samt Bildverweisen sofort mit, erst danach dürfen die Bilder weg.
            await kontext.Entry(v).Collection(x => x.Bloecke).Query().Include(b => b.Bilder).LoadAsync(abbruch);
            var bildIds = v.Bloecke.SelectMany(b => b.Bilder).Select(b => b.BildId).ToList();
            kontext.Veranstaltungen.Remove(v);
            await BilderLoeschenAsync(kontext, bildIds, id, abbruch);
            await kontext.SaveChangesAsync(abbruch);
            return VerwaltungsErgebnis.Ok(id);
        }

        public async Task<IReadOnlyList<EmpfaengerAnzeige>> EmpfaengerAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var v = await kontext.Veranstaltungen.AsNoTracking()
                .Include(x => x.Abteilung)
                .Include(x => x.BenachrichtigungEmpfaenger).ThenInclude(e => e.User)
                .SingleOrDefaultAsync(x => x.Id == veranstaltungId, abbruch);
            if (v is null || !VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.Abteilung?.Name))
                return [];

            return v.BenachrichtigungEmpfaenger
                .Select(e => new EmpfaengerAnzeige(
                    e.Id,
                    e.User is { } user ? Anzeigename(user) : e.Name ?? e.Email!,
                    e.User?.Email ?? e.Email,
                    e.UserId,
                    e.UserId != null && e.UserId == v.ErstelltVon,
                    e.Ereignisse,
                    e.Modus,
                    e.AbgemeldetUtc != null))
                .OrderByDescending(e => e.IstErsteller)
                .ThenBy(e => e.Anzeigename)
                .ToList();
        }

        public async Task<IReadOnlyList<BenutzerAuswahl>> MoeglicheEmpfaengerAsync(CancellationToken abbruch = default)
        {
            var benutzer = new Dictionary<string, ApplicationUser>();
            foreach (var rolle in EmpfaengerRollen)
                foreach (var user in await _userManager.GetUsersInRoleAsync(rolle))
                    benutzer.TryAdd(user.Id, user);

            return benutzer.Values
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => new BenutzerAuswahl(u.Id, Anzeigename(u), u.Email))
                .OrderBy(u => u.Anzeigename)
                .ToList();
        }

        public async Task<VerwaltungsErgebnis> EmpfaengerHinzufuegenAsync(int veranstaltungId, string? userId, string? email, string? name, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            if (string.IsNullOrWhiteSpace(userId) == string.IsNullOrWhiteSpace(email))
                return VerwaltungsErgebnis.MitFehler("Bitte entweder einen Benutzer oder eine E-Mail-Adresse angeben.");
            if (email is not null && !EmailAdresse.IstGueltig(email))
                return VerwaltungsErgebnis.MitFehler(Messages.EmailFormatErrorMessage);

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (v, fehler) = await LadenFuerEmpfaengerAsync(kontext, veranstaltungId, benutzer, abbruch);
            if (v is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);

            var empfaenger = new BenachrichtigungEmpfaenger
            {
                VeranstaltungId = v.Id,
                Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
                HinzugefuegtVon = benutzer.UserId,
                ErstelltUtc = JetztUtc,
                // Nur künftige Ereignisse melden
                BenachrichtigtBisUtc = JetztUtc
            };

            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (await _userManager.FindByIdAsync(userId) is null)
                    return VerwaltungsErgebnis.MitFehler("Den gewählten Benutzer gibt es nicht.");
                if (v.BenachrichtigungEmpfaenger.Any(e => e.UserId == userId))
                    return VerwaltungsErgebnis.MitFehler("Dieser Benutzer ist bereits eingetragen.");
                empfaenger.UserId = userId;
            }
            else
            {
                var adresse = EmailAdresse.Normalisieren(email!);
                if (v.BenachrichtigungEmpfaenger.Any(e => e.Email == adresse))
                    return VerwaltungsErgebnis.MitFehler("Diese E-Mail-Adresse ist bereits eingetragen.");
                empfaenger.Email = adresse;

                // Freie Adressen erfahren, wer sie eingetragen hat, und können sich abmelden (Plan 2.9)
                var token = AnmeldeToken.Erzeugen();
                empfaenger.AbmeldeTokenHash = token.Hash;
                var inhalt = BenachrichtigungMailVorlagen.Eingetragen(v, benutzer.Anzeigename, empfaenger.Ereignisse, empfaenger.Modus,
                    VeranstaltungLinks.BenachrichtigungAbmeldenUrl(_optionen.BasisUrl, token.Klartext));
                _warteschlange.Hinzufuegen(kontext, new AusgehendeEmail(adresse, inhalt.Betreff, inhalt.Html)
                {
                    AntwortAn = v.KontaktEmail,
                    Prioritaet = EmailPrioritaet.Normal,
                    BezugTyp = BenachrichtigungJob.BezugTyp
                });
            }

            kontext.BenachrichtigungEmpfaenger.Add(empfaenger);
            await kontext.SaveChangesAsync(abbruch);
            if (empfaenger.Email is not null)
                _warteschlange.VersandAnstossen();
            return VerwaltungsErgebnis.Ok(empfaenger.Id);
        }

        public async Task<VerwaltungsErgebnis> EmpfaengerAendernAsync(int veranstaltungId, int empfaengerId, BenachrichtigungEreignisse ereignisse, BenachrichtigungModus modus, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (v, fehler) = await LadenFuerEmpfaengerAsync(kontext, veranstaltungId, benutzer, abbruch);
            if (v is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);

            var empfaenger = v.BenachrichtigungEmpfaenger.SingleOrDefault(e => e.Id == empfaengerId);
            if (empfaenger is null)
                return VerwaltungsErgebnis.MitFehler("Der Empfänger wurde nicht gefunden.");

            empfaenger.Ereignisse = ereignisse & BenachrichtigungEreignisse.Alle;
            empfaenger.Modus = modus;
            await kontext.SaveChangesAsync(abbruch);
            return VerwaltungsErgebnis.Ok(empfaenger.Id);
        }

        public async Task<VerwaltungsErgebnis> EmpfaengerEntfernenAsync(int veranstaltungId, int empfaengerId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var (v, fehler) = await LadenFuerEmpfaengerAsync(kontext, veranstaltungId, benutzer, abbruch);
            if (v is null)
                return VerwaltungsErgebnis.MitFehler(fehler!);

            var empfaenger = v.BenachrichtigungEmpfaenger.SingleOrDefault(e => e.Id == empfaengerId);
            if (empfaenger is null)
                return VerwaltungsErgebnis.MitFehler("Der Empfänger wurde nicht gefunden.");

            kontext.BenachrichtigungEmpfaenger.Remove(empfaenger);
            await kontext.SaveChangesAsync(abbruch);
            return VerwaltungsErgebnis.Ok(empfaengerId);
        }

        public async Task<VerwaltungsErgebnis> BildHochladenAsync(Stream daten, string dateiname, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            if (!VeranstaltungRechte.DarfModulNutzen(benutzer))
                return VerwaltungsErgebnis.MitFehler(KeineBerechtigung);

            byte[] jpeg;
            try
            {
                jpeg = await BildKomprimierung.AlsJpegAsync(daten, abbruch);
            }
            catch (Exception ex) when (ex is SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
            {
                return VerwaltungsErgebnis.MitFehler($"„{Path.GetFileName(dateiname)}“ ist kein unterstütztes Bild (JPG, PNG oder WebP).");
            }

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            // Vorläufig bis zum Speichern der Veranstaltung, sonst räumt BildAufraeumJob es weg
            var bild = new DbImage { Title = Path.GetFileName(dateiname), ImageData = jpeg, ContentType = "image/jpeg", CreatedAt = JetztUtc, VorlaeufigSeitUtc = JetztUtc };
            kontext.Images.Add(bild);
            await kontext.SaveChangesAsync(abbruch);
            return VerwaltungsErgebnis.Ok(bild.Id);
        }

        // ---------------------------------------------------------------------------------------------

        private static string Anzeigename(ApplicationUser user)
        {
            var name = $"{user.Vorname} {user.Name}".Trim();
            return string.IsNullOrEmpty(name) ? user.UserName ?? user.Email ?? user.Id : name;
        }

        private async Task<(Veranstaltung? Veranstaltung, string? Fehler)> LadenFuerAktionAsync(
            ApplicationDbContext kontext, int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch)
        {
            var v = await kontext.Veranstaltungen
                .Include(x => x.Abteilung)
                .Include(x => x.Tage)
                .SingleOrDefaultAsync(x => x.Id == id, abbruch);
            if (v is null)
                return (null, NichtGefunden);
            if (!VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.Abteilung?.Name))
                return (null, KeineBerechtigung);
            return (v, null);
        }

        private async Task<(Veranstaltung? Veranstaltung, string? Fehler)> LadenFuerEmpfaengerAsync(
            ApplicationDbContext kontext, int id, VerwaltungsBenutzer benutzer, CancellationToken abbruch)
        {
            var v = await kontext.Veranstaltungen
                .Include(x => x.Abteilung)
                .Include(x => x.BenachrichtigungEmpfaenger)
                .SingleOrDefaultAsync(x => x.Id == id, abbruch);
            if (v is null)
                return (null, NichtGefunden);
            if (!VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.Abteilung?.Name))
                return (null, KeineBerechtigung);
            return (v, null);
        }

        /// <summary>Formale Prüfung der Eingabe, die nicht vom Ist-Stand abhängt.</summary>
        private static List<string> PruefeEingabe(VeranstaltungEingabe eingabe)
        {
            var fehler = new List<string>();

            if (string.IsNullOrWhiteSpace(eingabe.Titel))
                fehler.Add("Bitte einen Titel angeben.");

            // Mehrere Termine pro Datum sind erlaubt, aber nicht zweimal derselbe
            foreach (var doppelt in eingabe.Tage.GroupBy(t => (t.Datum, t.Beginn, Titel: t.Titel?.Trim() ?? "")).Where(g => g.Count() > 1))
                fehler.Add($"Der Termin {TerminText.Kurz(doppelt.First(), eingabe.Tage)} ist doppelt angelegt.");

            foreach (var tag in eingabe.Tage.Where(t => t.Ende <= t.Beginn))
                fehler.Add($"Beim Termin {TerminText.Kurz(tag, eingabe.Tage)} liegt das Ende nicht nach dem Beginn.");

            if (eingabe.KontaktEmail is { Length: > 0 } kontakt && !EmailAdresse.IstGueltig(kontakt))
                fehler.Add("Kontakt-E-Mail ist keine gültige E-Mail-Adresse.");

            return fehler;
        }

        /// <summary>Slug aus Eingabe oder Titel, eindeutig gemacht. Nach der ersten Veröffentlichung bleibt er unverändert.</summary>
        private async Task<string?> SlugErmittelnAsync(ApplicationDbContext kontext, Veranstaltung v, VeranstaltungEingabe eingabe, CancellationToken abbruch)
        {
            var basis = _slugService.GenerateSlug(string.IsNullOrWhiteSpace(eingabe.Slug) ? eingabe.Titel : eingabe.Slug);
            if (string.IsNullOrEmpty(basis))
                return null;

            // Unverändert oder gesperrt: nicht neu durchnummerieren (die Änderungsregeln melden einen gesperrten Slug)
            if (v.Id != 0 && (basis == v.Slug || v.ErstmalsVeroeffentlichtUtc != null))
                return basis;

            var slug = basis;
            for (var nummer = 2;
                 VeranstaltungLinks.ReservierteSlugs.Contains(slug) || await kontext.Veranstaltungen.AnyAsync(x => x.Slug == slug && x.Id != v.Id, abbruch);
                 nummer++)
            {
                slug = $"{basis}-{nummer}";
            }
            return slug;
        }

        private IReadOnlyList<string> PruefeAenderungsRegeln(Veranstaltung v, VeranstaltungEingabe eingabe, string neuerSlug)
        {
            if (v.Id == 0)
                return [];

            var jetzt = JetztUtc;
            var tage = v.Tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList();
            var belegungen = v.Anmeldungen
                .Select(a => new AnmeldungBelegung(a.Id, a.Status, a.ReserviertBisUtc, a.AnzahlBegleitpersonen, a.Tage.Select(at => at.VeranstaltungsTagId).ToList()))
                .ToList();
            var belegt = KapazitaetsRechner.BelegungProTag(v.Teilnahmemodus, tage, belegungen, jetzt);
            var aktive = v.Anmeldungen.Where(a => AnmeldungStatusUebergaenge.IstAktiv(a.Status)).ToList();

            var tagAenderungen = v.Tage
                .Where(t => !t.Abgesagt)
                .Select(t =>
                {
                    var neu = eingabe.Tage.SingleOrDefault(e => e.Id == t.Id);
                    var anmeldungenAmTag = aktive.Count(a => KapazitaetsRechner
                        .GueltigeTage(v.Teilnahmemodus, tage, a.Tage.Select(at => at.VeranstaltungsTagId).ToList())
                        .Contains(t.Id));
                    return new TagAenderung(t.Id, TerminText.Kurz(t, v.Tage), Entfernen: neu is null, neu?.MaxTeilnehmer, belegt[t.Id], anmeldungenAmTag);
                })
                .ToList();

            return VeranstaltungAenderungsRegeln.Pruefen(new VeranstaltungAenderung(
                v.Teilnahmemodus,
                eingabe.Teilnahmemodus,
                v.Slug,
                neuerSlug,
                v.ErstmalsVeroeffentlichtUtc != null,
                aktive.Count,
                tagAenderungen));
        }

        /// <summary>Gleicht die Tage mit der Eingabe ab. Abgesagte Tage bleiben unverändert.</summary>
        private static string? TageUebernehmen(ApplicationDbContext kontext, Veranstaltung v, IReadOnlyList<TagEingabe> eingaben)
        {
            if (eingaben.Any(e => e.Id is { } id && v.Tage.All(t => t.Id != id)))
                return "Ein Termin gehört nicht zu dieser Veranstaltung. Bitte die Seite neu laden.";

            foreach (var tag in v.Tage.Where(t => !t.Abgesagt && eingaben.All(e => e.Id != t.Id)).ToList())
            {
                v.Tage.Remove(tag);
                kontext.VeranstaltungsTage.Remove(tag);
            }

            foreach (var e in eingaben)
            {
                var tag = e.Id is { } id ? v.Tage.Single(t => t.Id == id) : null;
                if (tag is null)
                {
                    tag = new VeranstaltungsTag();
                    v.Tage.Add(tag);
                }
                else if (tag.Abgesagt)
                {
                    continue;
                }

                tag.Datum = e.Datum;
                tag.Beginn = e.Beginn;
                tag.Ende = e.Ende;
                tag.Titel = string.IsNullOrWhiteSpace(e.Titel) ? null : e.Titel.Trim();
                tag.MaxTeilnehmer = e.MaxTeilnehmer;
            }

            return null;
        }

        /// <summary>
        /// Schreibt die Bausteine neu. Neue Bilder müssen frisch hochgeladen sein (noch vorläufig und nirgends sonst verwendet):
        /// sonst ließe sich über die öffentliche Seite jedes Bild der Website freigeben. Entfernte Bilder werden samt Bilddaten gelöscht.
        /// </summary>
        private static async Task<string?> BloeckeUebernehmenAsync(ApplicationDbContext kontext, Veranstaltung v, IReadOnlyList<BlockEingabe> eingaben, CancellationToken abbruch)
        {
            var alte = v.Bloecke.SelectMany(b => b.Bilder).Select(b => b.BildId).ToHashSet();
            var neue = GalerieBildIds(eingaben).ToHashSet();

            var hinzugekommen = neue.Except(alte).ToList();
            if (hinzugekommen.Count > 0)
            {
                var vorhanden = await kontext.Images.CountAsync(i => hinzugekommen.Contains(i.Id) && i.VorlaeufigSeitUtc != null, abbruch);
                var verwendet = await BildVerwendung.VerwendeteBildIds(kontext).AnyAsync(id => hinzugekommen.Contains(id), abbruch);
                if (vorhanden != hinzugekommen.Count || verwendet)
                    return "Ein Bild gehört nicht zu dieser Veranstaltung. Bitte die Seite neu laden und das Bild erneut hochladen.";
            }

            foreach (var block in v.Bloecke.ToList())
            {
                v.Bloecke.Remove(block);
                kontext.VeranstaltungBloecke.Remove(block);
            }

            var sortierung = 0;
            foreach (var e in eingaben)
            {
                var istText = e.Typ == VeranstaltungBlockTyp.MarkdownText;
                v.Bloecke.Add(new VeranstaltungBlock
                {
                    Typ = e.Typ,
                    Sortierung = sortierung++,
                    MarkdownInhalt = istText ? e.MarkdownInhalt ?? string.Empty : null,
                    BilderProReihe = Math.Clamp(e.BilderProReihe, 1, 6),
                    BildUnterschrift = istText || string.IsNullOrWhiteSpace(e.BildUnterschrift) ? null : e.BildUnterschrift.Trim(),
                    Bilder = istText ? [] : e.BildIds.Distinct().Select((id, i) => new VeranstaltungBild { BildId = id, Sortierung = i }).ToList()
                });
            }

            await BilderLoeschenAsync(kontext, alte.Except(neue).ToList(), v.Id, abbruch);
            return null;
        }

        /// <summary>Löscht die Bilddaten, sofern nicht noch anderswo auf sie verwiesen wird.</summary>
        private static async Task BilderLoeschenAsync(ApplicationDbContext kontext, IReadOnlyCollection<int> bildIds, int veranstaltungId, CancellationToken abbruch)
        {
            if (bildIds.Count == 0)
                return;
            var nochVerwendet = await BildVerwendung.VerwendeteBildIds(kontext, veranstaltungId).Where(id => bildIds.Contains(id)).ToListAsync(abbruch);
            var loeschen = await kontext.Images.Where(i => bildIds.Contains(i.Id) && !nochVerwendet.Contains(i.Id)).ToListAsync(abbruch);
            kontext.Images.RemoveRange(loeschen);
        }

        private static List<int> GalerieBildIds(IEnumerable<BlockEingabe> bloecke) =>
            bloecke.Where(b => b.Typ == VeranstaltungBlockTyp.BilderGalerie).SelectMany(b => b.BildIds).Distinct().ToList();

        private static void FelderUebernehmen(Veranstaltung v, VeranstaltungEingabe e, string slug)
        {
            v.Titel = e.Titel.Trim();
            v.Slug = slug;
            v.Kurzbeschreibung = Leer(e.Kurzbeschreibung);
            v.Ort = Leer(e.Ort);
            v.Adresse = Leer(e.Adresse);
            v.KartenLink = Leer(e.KartenLink);
            v.AbteilungId = Leer(e.AbteilungId);
            v.KontaktName = Leer(e.KontaktName);
            v.KontaktEmail = string.IsNullOrWhiteSpace(e.KontaktEmail) ? null : EmailAdresse.Normalisieren(e.KontaktEmail);
            v.Sichtbarkeit = e.Sichtbarkeit;
            v.AnmeldungAb = e.AnmeldungAb;
            v.AnmeldungBis = e.AnmeldungBis;
            v.AenderungenBis = e.AenderungenBis;
            v.Teilnahmemodus = e.Teilnahmemodus;
            v.MinTageBeiTeilanmeldung = e.MinTageBeiTeilanmeldung;
            v.MaxTeilnehmerVorgabe = e.MaxTeilnehmerVorgabe;
            v.MaxBegleitpersonen = e.MaxBegleitpersonen;
            v.DoubleOptIn = e.DoubleOptIn;
            v.TelefonFeld = e.TelefonFeld;
            v.VereinFeld = e.VereinFeld;
            v.GraduierungFeld = e.GraduierungFeld;
            v.BemerkungFeld = e.BemerkungFeld;
            v.ZusammenfassungUhrzeit = e.ZusammenfassungUhrzeit;
        }

        private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();

        /// <summary>
        /// Speichert die Veranstaltung und gleicht danach die Kalendereinträge ab (dafür werden die Ids neuer Tage gebraucht),
        /// beides in einer Transaktion. Erkennt gleichzeitige Änderungen über die RowVersion.
        /// </summary>
        /// <param name="bildIds">Bilder der gespeicherten Galerien: sind danach nicht mehr vorläufig (BildVerwendung).</param>
        private async Task<VerwaltungsErgebnis> SpeichernMitKalenderAsync(ApplicationDbContext kontext, Veranstaltung v, CancellationToken abbruch, IReadOnlyCollection<int>? bildIds = null)
        {
            try
            {
                await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
                await kontext.SaveChangesAsync(abbruch);
                await BildVerwendung.AlsGespeichertMarkierenAsync(kontext, bildIds ?? [], abbruch);

                await kontext.Entry(v).Reference(x => x.Abteilung).LoadAsync(abbruch);
                await KalenderAbgleich.AbgleichenAsync(kontext, v, Ortszeit.Jetzt(_zeit), abbruch);
                await kontext.SaveChangesAsync(abbruch);

                await transaktion.CommitAsync(abbruch);
                return VerwaltungsErgebnis.Ok(v.Id);
            }
            catch (DbUpdateConcurrencyException)
            {
                return VerwaltungsErgebnis.MitFehler("Die Veranstaltung wurde inzwischen von jemand anderem geändert. Bitte die Seite neu laden.");
            }
            catch (DbUpdateException)
            {
                // Z. B. eindeutiger Slug, wenn zwei Organisatoren gleichzeitig dieselbe Adresse vergeben
                return VerwaltungsErgebnis.MitFehler("Die Änderungen konnten nicht gespeichert werden (Adresse bereits vergeben). Bitte prüfen und erneut speichern.");
            }
        }
    }
}
