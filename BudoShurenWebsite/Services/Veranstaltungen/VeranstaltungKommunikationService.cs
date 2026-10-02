using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    public sealed record NachrichtAnzeige(int Id, NachrichtArt Art, string Betreff, int AnzahlEmpfaenger, bool AnInfoAdressen, DateTime GesendetUtc, string? Absender);

    public interface IVeranstaltungKommunikationService
    {
        Task<IReadOnlyList<NachrichtAnzeige>> NachrichtenAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>An alle bestätigten Teilnehmer, auf Wunsch auch an ihre Info-Adressen. Ok-Id = Id der protokollierten Nachricht.</summary>
        Task<VerwaltungsErgebnis> RundmailSendenAsync(int veranstaltungId, string betreff, string inhaltMarkdown, bool anInfoAdressen, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Vorschau an eine Adresse (z. B. die eigene), ohne Protokoll.</summary>
        Task<VerwaltungsErgebnis> TestmailSendenAsync(int veranstaltungId, string betreff, string inhaltMarkdown, string an, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Sagt einen Tag ab (bleibt erhalten, zählt nicht mehr), entfernt ihn aus dem Kalender und informiert die Betroffenen.</summary>
        Task<VerwaltungsErgebnis> TagAbsagenAsync(int veranstaltungId, int tagId, string? zusatzMarkdown, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);

        /// <summary>Sagt die ganze Veranstaltung ab, entfernt sie aus dem Kalender und informiert alle Angemeldeten.</summary>
        Task<VerwaltungsErgebnis> AbsagenAsync(int veranstaltungId, string? zusatzMarkdown, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default);
    }

    /// <summary>
    /// Nachrichten der Organisatoren an Teilnehmer und Info-Adressen. Jede Mail geht einzeln über die Warteschlange
    /// (persönliche Anrede bzw. eigener Abmeldelink), jede Adresse höchstens einmal. Versendete Nachrichten werden protokolliert.
    /// </summary>
    public sealed class VeranstaltungKommunikationService : IVeranstaltungKommunikationService
    {
        private const string KeineBerechtigung = "Du darfst diese Veranstaltung nicht verwalten.";
        private const int MaxBetreff = 200;
        private const int MaxInhalt = 20000;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly AnmeldungMailVersand _mails;
        private readonly TimeProvider _zeit;

        public VeranstaltungKommunikationService(IDbContextFactory<ApplicationDbContext> dbFactory, AnmeldungMailVersand mails, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _mails = mails;
            _zeit = zeit;
        }

        private DateTime JetztUtc => _zeit.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<NachrichtAnzeige>> NachrichtenAsync(int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (await LadenAsync(kontext, veranstaltungId, benutzer, abbruch) is not { } v)
                return [];

            var nachrichten = await kontext.VeranstaltungNachrichten.AsNoTracking()
                .Where(n => n.VeranstaltungId == v.Id)
                .OrderByDescending(n => n.GesendetUtc).ThenByDescending(n => n.Id)
                .ToListAsync(abbruch);
            var ids = nachrichten.Select(n => n.ErstelltVon).Distinct().ToList();
            var namen = await kontext.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.Vorname} {u.Name}".Trim(), abbruch);

            return nachrichten
                .Select(n => new NachrichtAnzeige(n.Id, n.Art, n.Betreff, n.AnzahlEmpfaenger, n.AnInfoAdressen, n.GesendetUtc, namen.GetValueOrDefault(n.ErstelltVon)))
                .ToList();
        }

        public async Task<VerwaltungsErgebnis> RundmailSendenAsync(int veranstaltungId, string betreff, string inhaltMarkdown, bool anInfoAdressen, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            if (PruefeText(betreff, inhaltMarkdown, inhaltPflicht: true) is { } textFehler)
                return VerwaltungsErgebnis.MitFehler(textFehler);

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (await LadenAsync(kontext, veranstaltungId, benutzer, abbruch, mitAnmeldungen: true) is not { } v)
                return VerwaltungsErgebnis.MitFehler(KeineBerechtigung);
            if (v.Status == VeranstaltungStatus.Entwurf)
                return VerwaltungsErgebnis.MitFehler("Entwürfe haben keine Teilnehmer.");

            var empfaenger = v.Anmeldungen.Where(a => a.Status == AnmeldungStatus.Angemeldet).ToList();
            var anzahl = Versenden(kontext, v, empfaenger, anInfoAdressen ? empfaenger : [], betreff.Trim(), MarkdownText.SicherZuHtml(inhaltMarkdown), basisUrl);
            if (anzahl == 0)
                return VerwaltungsErgebnis.MitFehler("Es gibt noch keine bestätigten Teilnehmer.");

            var nachricht = Protokollieren(kontext, v, NachrichtArt.Rundmail, betreff.Trim(), inhaltMarkdown, anInfoAdressen, anzahl, benutzer);
            await kontext.SaveChangesAsync(abbruch);
            _mails.VersandAnstossen();
            return VerwaltungsErgebnis.Ok(nachricht.Id);
        }

        public async Task<VerwaltungsErgebnis> TestmailSendenAsync(int veranstaltungId, string betreff, string inhaltMarkdown, string an, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            if (PruefeText(betreff, inhaltMarkdown, inhaltPflicht: true) is { } textFehler)
                return VerwaltungsErgebnis.MitFehler(textFehler);
            if (!EmailAdresse.IstGueltig(an))
                return VerwaltungsErgebnis.MitFehler("Für die Testmail fehlt eine gültige Adresse.");

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (await LadenAsync(kontext, veranstaltungId, benutzer, abbruch) is not { } v)
                return VerwaltungsErgebnis.MitFehler(KeineBerechtigung);

            var inhalt = VeranstaltungMailVorlagen.Nachricht(v, $"[Test] {betreff.Trim()}", MarkdownText.SicherZuHtml(inhaltMarkdown), benutzer.Anzeigename,
                VeranstaltungMailVorlagen.FussTeilnehmer(VeranstaltungLinks.LinkAnfordernUrl(basisUrl)));
            _mails.Allgemein(kontext, EmailAdresse.Normalisieren(an), inhalt);
            await kontext.SaveChangesAsync(abbruch);
            _mails.VersandAnstossen();
            return VerwaltungsErgebnis.Ok(v.Id);
        }

        public async Task<VerwaltungsErgebnis> TagAbsagenAsync(int veranstaltungId, int tagId, string? zusatzMarkdown, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            if (zusatzMarkdown?.Length > MaxInhalt)
                return VerwaltungsErgebnis.MitFehler("Der Text ist zu lang.");

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (!await DarfVerwaltenAsync(kontext, veranstaltungId, benutzer, abbruch))
                return VerwaltungsErgebnis.MitFehler(KeineBerechtigung);

            await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            await VeranstaltungSperre.SetzenAsync(kontext, veranstaltungId, abbruch);
            var v = (await LadenAsync(kontext, veranstaltungId, benutzer, abbruch, mitAnmeldungen: true))!;

            var tag = v.Tage.SingleOrDefault(t => t.Id == tagId);
            if (tag is null)
                return VerwaltungsErgebnis.MitFehler("Der Termin gehört nicht zu dieser Veranstaltung.");
            if (tag.Abgesagt)
                return VerwaltungsErgebnis.MitFehler("Der Termin ist bereits abgesagt.");
            if (v.Status != VeranstaltungStatus.Veroeffentlicht)
                return VerwaltungsErgebnis.MitFehler("Termine können nur bei veröffentlichten Veranstaltungen abgesagt werden. Im Entwurf den Termin einfach entfernen.");
            if (v.Tage.Count(t => !t.Abgesagt) == 1)
                return VerwaltungsErgebnis.MitFehler("Das ist der letzte Termin. Bitte stattdessen die ganze Veranstaltung absagen.");

            // Betroffen sind aktive Anmeldungen, die für diesen Termin gelten (vor der Absage berechnet)
            var tagKapazitaeten = v.Tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList();
            var betroffen = v.Anmeldungen
                .Where(a => AnmeldungStatusUebergaenge.IstAktiv(a.Status)
                            && KapazitaetsRechner.GueltigeTage(v.Teilnahmemodus, tagKapazitaeten, a.Tage.Select(t => t.VeranstaltungsTagId).ToList()).Contains(tag.Id))
                .ToList();

            var jetzt = JetztUtc;
            tag.Abgesagt = true;
            v.GeaendertVon = benutzer.UserId;
            v.GeaendertUtc = jetzt;
            foreach (var a in betroffen)
            {
                AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.TagAbgesagt, EreignisAkteur.Admin, jetzt,
                    EreignisDiff.Wert(TerminText.Kurz(tag, v.Tage, mitTitel: true), null), benutzer.UserId);
            }

            var betreff = $"Termin abgesagt: {v.Titel}";
            var inhalt = VeranstaltungMailVorlagen.TagAbgesagtInhalt(tag, Zusatz(zusatzMarkdown));
            var anzahl = Versenden(kontext, v, betroffen, betroffen.Where(a => a.Status == AnmeldungStatus.Angemeldet), betreff, inhalt, basisUrl);
            Protokollieren(kontext, v, NachrichtArt.TagAbgesagt, betreff, zusatzMarkdown ?? string.Empty, anInfoAdressen: true, anzahl, benutzer);

            await KalenderAbgleich.AbgleichenAsync(kontext, v, Ortszeit.Jetzt(_zeit), abbruch);
            await kontext.SaveChangesAsync(abbruch);
            await transaktion.CommitAsync(abbruch);
            _mails.VersandAnstossen();
            return VerwaltungsErgebnis.Ok(tag.Id);
        }

        public async Task<VerwaltungsErgebnis> AbsagenAsync(int veranstaltungId, string? zusatzMarkdown, string basisUrl, VerwaltungsBenutzer benutzer, CancellationToken abbruch = default)
        {
            if (zusatzMarkdown?.Length > MaxInhalt)
                return VerwaltungsErgebnis.MitFehler("Der Text ist zu lang.");

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            if (!await DarfVerwaltenAsync(kontext, veranstaltungId, benutzer, abbruch))
                return VerwaltungsErgebnis.MitFehler(KeineBerechtigung);

            // Unter der Sperre: keine Anmeldung kann gleichzeitig noch durchrutschen
            await using var transaktion = await kontext.Database.BeginTransactionAsync(abbruch);
            await VeranstaltungSperre.SetzenAsync(kontext, veranstaltungId, abbruch);
            var v = (await LadenAsync(kontext, veranstaltungId, benutzer, abbruch, mitAnmeldungen: true))!;
            if (v.Status != VeranstaltungStatus.Veroeffentlicht)
                return VerwaltungsErgebnis.MitFehler("Nur veröffentlichte Veranstaltungen können abgesagt werden.");

            var jetzt = JetztUtc;
            v.Status = VeranstaltungStatus.Abgesagt;
            v.GeaendertVon = benutzer.UserId;
            v.GeaendertUtc = jetzt;

            var betroffen = v.Anmeldungen.Where(a => AnmeldungStatusUebergaenge.IstAktiv(a.Status)).ToList();
            var betreff = $"Abgesagt: {v.Titel}";
            var anzahl = Versenden(kontext, v, betroffen, betroffen.Where(a => a.Status == AnmeldungStatus.Angemeldet), betreff,
                VeranstaltungMailVorlagen.AbgesagtInhalt(Zusatz(zusatzMarkdown)), basisUrl);
            Protokollieren(kontext, v, NachrichtArt.VeranstaltungAbgesagt, betreff, zusatzMarkdown ?? string.Empty, anInfoAdressen: true, anzahl, benutzer);

            await KalenderAbgleich.AbgleichenAsync(kontext, v, Ortszeit.Jetzt(_zeit), abbruch);
            await kontext.SaveChangesAsync(abbruch);
            await transaktion.CommitAsync(abbruch);
            _mails.VersandAnstossen();
            return VerwaltungsErgebnis.Ok(v.Id);
        }

        // ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Rechteprüfung ohne Tracking: vor der Sperre darf nichts in den Kontext geladen werden, sonst sähe das Laden
        /// unter der Sperre die bereits getrackten (womöglich veralteten) Werte statt des aktuellen Stands.
        /// </summary>
        private static async Task<bool> DarfVerwaltenAsync(ApplicationDbContext kontext, int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch)
        {
            var v = await kontext.Veranstaltungen.AsNoTracking()
                .Where(x => x.Id == veranstaltungId)
                .Select(x => new { x.AbteilungId, AbteilungName = x.Abteilung != null ? x.Abteilung.Name : null })
                .SingleOrDefaultAsync(abbruch);
            return v is not null && VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.AbteilungName);
        }

        /// <summary>Veranstaltung mit Tagen und Abteilung (für Rechte und Kalender), optional mit allen Anmeldungen; null ohne Berechtigung.</summary>
        private static async Task<Veranstaltung?> LadenAsync(ApplicationDbContext kontext, int veranstaltungId, VerwaltungsBenutzer benutzer, CancellationToken abbruch, bool mitAnmeldungen = false)
        {
            var abfrage = kontext.Veranstaltungen.Include(x => x.Abteilung).Include(x => x.Tage).AsQueryable();
            if (mitAnmeldungen)
                abfrage = abfrage
                    .Include(x => x.Anmeldungen).ThenInclude(a => a.Tage)
                    .Include(x => x.Anmeldungen).ThenInclude(a => a.InfoEmails)
                    .AsSplitQuery();
            var v = await abfrage.SingleOrDefaultAsync(x => x.Id == veranstaltungId, abbruch);
            return v is not null && VeranstaltungRechte.DarfVerwalten(benutzer, v.AbteilungId, v.Abteilung?.Name) ? v : null;
        }

        private static string? PruefeText(string? betreff, string? inhalt, bool inhaltPflicht)
        {
            if (string.IsNullOrWhiteSpace(betreff))
                return "Bitte einen Betreff angeben.";
            if (betreff.Trim().Length > MaxBetreff)
                return $"Der Betreff darf höchstens {MaxBetreff} Zeichen lang sein.";
            if (inhaltPflicht && string.IsNullOrWhiteSpace(inhalt))
                return "Bitte einen Text eingeben.";
            if (inhalt?.Length > MaxInhalt)
                return "Der Text ist zu lang.";
            return null;
        }

        private static string? Zusatz(string? markdown) => string.IsNullOrWhiteSpace(markdown) ? null : MarkdownText.SicherZuHtml(markdown);

        /// <summary>
        /// Je Teilnehmer und je (nicht abgemeldeter) Info-Adresse eine eigene Mail, jede Adresse höchstens einmal.
        /// Info-Adressen bekommen einen neuen Abmeldelink. Liefert die Zahl der Empfänger.
        /// </summary>
        private int Versenden(ApplicationDbContext kontext, Veranstaltung v, IEnumerable<Anmeldung> teilnehmer, IEnumerable<Anmeldung> mitInfoAdressen,
            string betreff, string inhaltHtml, string basisUrl)
        {
            var schonBedient = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fussTeilnehmer = VeranstaltungMailVorlagen.FussTeilnehmer(VeranstaltungLinks.LinkAnfordernUrl(basisUrl));

            foreach (var a in teilnehmer.Where(a => schonBedient.Add(a.Email)))
                _mails.AnTeilnehmer(kontext, v, a, VeranstaltungMailVorlagen.Nachricht(v, betreff, inhaltHtml, a.Vorname, fussTeilnehmer), EmailPrioritaet.Normal);

            foreach (var a in mitInfoAdressen)
            {
                foreach (var info in a.InfoEmails.Where(i => i.AbgemeldetUtc is null && schonBedient.Add(i.Email)))
                {
                    var abmelden = AnmeldeToken.Erzeugen();
                    info.AbmeldeTokenHash = abmelden.Hash;
                    _mails.AnTeilnehmer(kontext, v, a,
                        VeranstaltungMailVorlagen.Nachricht(v, betreff, inhaltHtml, null, VeranstaltungMailVorlagen.FussInfo(VeranstaltungLinks.InfoAbmeldenUrl(basisUrl, abmelden.Klartext))),
                        EmailPrioritaet.Normal, an: info.Email);
                }
            }

            return schonBedient.Count;
        }

        private VeranstaltungNachricht Protokollieren(ApplicationDbContext kontext, Veranstaltung v, NachrichtArt art, string betreff, string inhalt, bool anInfoAdressen, int anzahl, VerwaltungsBenutzer benutzer)
        {
            var nachricht = new VeranstaltungNachricht
            {
                VeranstaltungId = v.Id,
                Art = art,
                Betreff = betreff.Length > MaxBetreff ? betreff[..MaxBetreff] : betreff,
                InhaltMarkdown = inhalt,
                AnInfoAdressen = anInfoAdressen,
                AnzahlEmpfaenger = anzahl,
                ErstelltVon = benutzer.UserId,
                GesendetUtc = JetztUtc
            };
            kontext.VeranstaltungNachrichten.Add(nachricht);
            return nachricht;
        }
    }
}
