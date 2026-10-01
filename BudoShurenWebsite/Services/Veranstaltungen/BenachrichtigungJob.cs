using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Organisator-Benachrichtigungen (Plan 2.9): wertet für jeden Empfänger die Ereignisse seit BenachrichtigtBisUtc aus
    /// (BenachrichtigungsAuswertung) und reiht die Mails ein. Läuft regelmäßig über VeranstaltungJobsHostedService;
    /// Tests rufen AusfuehrenAsync direkt auf. Nur eine Instanz gleichzeitig (die App läuft als einzelne Instanz).
    /// </summary>
    public sealed class BenachrichtigungJob
    {
        public const string BezugTyp = "Benachrichtigung";

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly IEmailWarteschlange _warteschlange;
        private readonly TimeProvider _zeit;
        private readonly VeranstaltungenOptionen _optionen;
        private readonly ILogger<BenachrichtigungJob> _logger;

        public BenachrichtigungJob(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            IEmailWarteschlange warteschlange,
            TimeProvider zeit,
            IOptions<VeranstaltungenOptionen> optionen,
            ILogger<BenachrichtigungJob> logger)
        {
            _dbFactory = dbFactory;
            _warteschlange = warteschlange;
            _zeit = zeit;
            _optionen = optionen.Value;
            _logger = logger;
        }

        /// <returns>Anzahl eingereihter Mails.</returns>
        public async Task<int> AusfuehrenAsync(CancellationToken abbruch)
        {
            List<int> ids;
            await using (var kontext = await _dbFactory.CreateDbContextAsync(abbruch))
            {
                // Nur veröffentlichte: in Entwürfen gibt es keine Anmeldungen, nach Absage oder Abschluss interessiert nichts mehr
                ids = await kontext.Veranstaltungen
                    .Where(v => v.Status == VeranstaltungStatus.Veroeffentlicht && v.BenachrichtigungEmpfaenger.Any())
                    .Select(v => v.Id)
                    .ToListAsync(abbruch);
            }

            var mails = 0;
            foreach (var id in ids)
                mails += await VeranstaltungAuswertenAsync(id, abbruch);

            if (mails > 0)
                _warteschlange.VersandAnstossen();
            return mails;
        }

        private async Task<int> VeranstaltungAuswertenAsync(int veranstaltungId, CancellationToken abbruch)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var jetzt = _zeit.GetUtcNow().UtcDateTime;

            var v = await kontext.Veranstaltungen
                .Include(x => x.Tage)
                .Include(x => x.BenachrichtigungEmpfaenger).ThenInclude(e => e.User)
                .AsSplitQuery()
                .SingleAsync(x => x.Id == veranstaltungId, abbruch);

            var seit = v.BenachrichtigungEmpfaenger.Min(e => e.BenachrichtigtBisUtc);
            var ereignisse = await kontext.AnmeldungEreignisse.AsNoTracking()
                .Where(e => e.Anmeldung!.VeranstaltungId == v.Id && e.ZeitpunktUtc > seit)
                .OrderBy(e => e.ZeitpunktUtc).ThenBy(e => e.Id)
                .Select(e => new BenachrichtigungEreignis(e.AnmeldungId, e.ZeitpunktUtc, e.Art, e.Akteur, e.AkteurUserId, e.DetailsJson))
                .ToListAsync(abbruch);

            var anmeldungen = await kontext.Anmeldungen.AsNoTracking()
                .Where(a => a.VeranstaltungId == v.Id)
                .Select(a => new AnmeldungKurz(a.Id, a.Vorname, a.Nachname, a.Status, a.ReserviertBisUtc, a.AnzahlBegleitpersonen,
                    a.Tage.Select(t => t.VeranstaltungsTagId).ToList()))
                .ToDictionaryAsync(a => a.Id, abbruch);

            var stand = Stand(v, anmeldungen.Values, jetzt, out var ausgebucht);
            var veranstaltungStand = new VeranstaltungStand(
                v.DoubleOptIn,
                v.AnmeldungBis is { } bis ? Ortszeit.NachUtc(bis) : null,
                v.ZusammenfassungUhrzeit,
                ausgebucht,
                anmeldungen.Values.Where(a => a.Status == AnmeldungStatus.Unbestaetigt).Select(a => a.Id).ToHashSet());

            var mails = 0;
            foreach (var empfaenger in v.BenachrichtigungEmpfaenger)
            {
                var plan = BenachrichtigungsAuswertung.Auswerten(
                    new EmpfaengerStand(empfaenger.Modus, empfaenger.Ereignisse, empfaenger.UserId, empfaenger.AbgemeldetUtc != null, empfaenger.BenachrichtigtBisUtc),
                    veranstaltungStand,
                    ereignisse,
                    jetzt);
                if (plan is null)
                    continue;

                empfaenger.BenachrichtigtBisUtc = plan.NeuBisUtc;
                if (plan.Senden && Einreihen(kontext, v, empfaenger, plan, anmeldungen, stand))
                    mails++;
            }

            await kontext.SaveChangesAsync(abbruch);
            return mails;
        }

        private bool Einreihen(ApplicationDbContext kontext, Veranstaltung v, BenachrichtigungEmpfaenger empfaenger, BenachrichtigungsPlan plan,
            IReadOnlyDictionary<int, AnmeldungKurz> anmeldungen, IReadOnlyList<TagStand> stand)
        {
            var istBenutzer = empfaenger.UserId is not null;
            var adresse = istBenutzer ? empfaenger.User?.Email : empfaenger.Email;
            if (string.IsNullOrWhiteSpace(adresse) || !EmailAdresse.IstGueltig(adresse))
            {
                _logger.LogWarning("Benachrichtigung für Veranstaltung {VeranstaltungId} übersprungen: Empfänger {EmpfaengerId} hat keine gültige Adresse",
                    v.Id, empfaenger.Id);
                return false;
            }

            var abschnitte = plan.Meldungen
                .GroupBy(m => m.AnmeldungId)
                .Where(g => anmeldungen.ContainsKey(g.Key))
                .Select(g =>
                {
                    var a = anmeldungen[g.Key];
                    var tage = v.Teilnahmemodus == Teilnahmemodus.EinzelneTage
                        ? v.Tage.Where(t => a.TagIds.Contains(t.Id)).Select(t => t.Datum).Order().ToList()
                        : [];
                    return new BenachrichtigungsAbschnitt(
                        $"{a.Vorname} {a.Nachname}",
                        1 + a.AnzahlBegleitpersonen,
                        tage,
                        g.Select(m => new BenachrichtigungsZeile(m.ZeitpunktUtc, Zeile(m))).ToList());
                })
                .ToList();

            // Freie Adressen bekommen in jeder Mail einen neuen Abmeldelink (gespeichert ist nur der Hash)
            string? abmeldenUrl = null;
            if (!istBenutzer)
            {
                var token = AnmeldeToken.Erzeugen();
                empfaenger.AbmeldeTokenHash = token.Hash;
                abmeldenUrl = VeranstaltungLinks.BenachrichtigungAbmeldenUrl(_optionen.BasisUrl, token.Klartext);
            }

            var inhalt = BenachrichtigungMailVorlagen.Benachrichtigung(
                v,
                empfaenger.Modus == BenachrichtigungModus.TaeglicheZusammenfassung,
                abschnitte,
                plan.Ausgebucht,
                plan.Anmeldeschluss,
                stand,
                istBenutzer ? VeranstaltungLinks.UebersichtUrl(_optionen.BasisUrl, v.Id) : null,
                abmeldenUrl);

            _warteschlange.Hinzufuegen(kontext, new AusgehendeEmail(adresse, inhalt.Betreff, inhalt.Html)
            {
                Prioritaet = EmailPrioritaet.Normal,
                BezugTyp = BezugTyp,
                BezugId = empfaenger.Id
            });
            return true;
        }

        private static string Zeile(BenachrichtigungEreignis e)
        {
            var text = EreignisText.FuerBenachrichtigung(e.Art, e.DetailsJson);
            return e.Akteur == EreignisAkteur.Admin ? $"{text} (durch Organisator)" : text;
        }

        /// <summary>Belegung je stattfindendem Tag; ausgebucht = niemand kann sich mehr anmelden.</summary>
        private static List<TagStand> Stand(Veranstaltung v, IEnumerable<AnmeldungKurz> anmeldungen, DateTime jetzt, out bool ausgebucht)
        {
            var tage = v.Tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList();
            var belegungen = anmeldungen.Select(a => new AnmeldungBelegung(a.Id, a.Status, a.ReserviertBisUtc, a.AnzahlBegleitpersonen, a.TagIds)).ToList();
            var belegt = KapazitaetsRechner.BelegungProTag(v.Teilnahmemodus, tage, belegungen, jetzt);
            var frei = KapazitaetsRechner.FreiePlaetzeProTag(v.Teilnahmemodus, tage, belegungen, jetzt);

            // Bei NurGesamt gilt eine Anmeldung für alle Tage: ein voller Tag reicht. Bei Teilanmeldung müssen alle voll sein.
            ausgebucht = frei.Count > 0 && (v.Teilnahmemodus == Teilnahmemodus.NurGesamt
                ? frei.Values.Any(f => f == 0)
                : frei.Values.All(f => f == 0));

            return v.Tage.Where(t => !t.Abgesagt).OrderBy(t => t.Datum).ThenBy(t => t.Beginn)
                .Select(t => new TagStand(t, belegt[t.Id], t.MaxTeilnehmer))
                .ToList();
        }

        private sealed record AnmeldungKurz(int Id, string Vorname, string Nachname, AnmeldungStatus Status, DateTime? ReserviertBisUtc,
            int AnzahlBegleitpersonen, List<int> TagIds);
    }
}
