using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <param name="FreiePlaetze">null = unbegrenzt.</param>
    public sealed record TagAnzeige(int Id, DateOnly Datum, TimeOnly Beginn, TimeOnly Ende, string? Titel, bool Abgesagt, int? FreiePlaetze);

    /// <summary>Was die öffentliche Seite einer Veranstaltung braucht (ohne Teilnehmerdaten).</summary>
    public sealed record VeranstaltungAnzeige(
        string Titel,
        string Slug,
        string? Kurzbeschreibung,
        string BeschreibungHtml,
        string? Ort,
        string? Adresse,
        string? KartenLink,
        string? Abteilung,
        string? KontaktName,
        string? KontaktEmail,
        VeranstaltungStatus Status,
        AnmeldeZustand Anmeldung,
        DateTime? AnmeldungAb,
        DateTime? AnmeldungBis,
        Teilnahmemodus Teilnahmemodus,
        int MinTageBeiTeilanmeldung,
        int MaxBegleitpersonen,
        bool DoubleOptIn,
        FormularFeldModus TelefonFeld,
        FormularFeldModus VereinFeld,
        FormularFeldModus GraduierungFeld,
        FormularFeldModus BemerkungFeld,
        IReadOnlyList<TagAnzeige> Tage)
    {
        public IEnumerable<TagAnzeige> AktiveTage => Tage.Where(t => !t.Abgesagt);

        public AnmeldeFormularEinstellungen Formular => new(
            Teilnahmemodus, MinTageBeiTeilanmeldung, MaxBegleitpersonen, TelefonFeld, VereinFeld, GraduierungFeld, BemerkungFeld);

        /// <summary>Bei Anmeldung für die gesamte Veranstaltung reicht ein voller Tag, sonst müssen alle Tage voll sein.</summary>
        public bool Ausgebucht => Teilnahmemodus == Teilnahmemodus.NurGesamt
            ? AktiveTage.Any(t => t.FreiePlaetze == 0)
            : AktiveTage.All(t => t.FreiePlaetze == 0);
    }

    public sealed record VeranstaltungKurz(string Titel, string Slug, string? Kurzbeschreibung, string? Ort, string? Abteilung, DateOnly ErsterTag, DateOnly LetzterTag);

    public interface IVeranstaltungAnzeigeService
    {
        /// <summary>Kommende, öffentliche, veröffentlichte Veranstaltungen.</summary>
        Task<IReadOnlyList<VeranstaltungKurz>> KommendeAsync(CancellationToken abbruch = default);

        /// <summary>null für unbekannte, Entwürfe und archivierte Veranstaltungen. "Nur per Link" ist über den Slug erreichbar.</summary>
        Task<VeranstaltungAnzeige?> LadenAsync(string slug, CancellationToken abbruch = default);
    }

    public sealed class VeranstaltungAnzeigeService : IVeranstaltungAnzeigeService
    {
        private static readonly VeranstaltungStatus[] Sichtbar =
            [VeranstaltungStatus.Veroeffentlicht, VeranstaltungStatus.Abgesagt, VeranstaltungStatus.Abgeschlossen];

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public VeranstaltungAnzeigeService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        public async Task<IReadOnlyList<VeranstaltungKurz>> KommendeAsync(CancellationToken abbruch = default)
        {
            var heute = DateOnly.FromDateTime(Ortszeit.Jetzt(_zeit));
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);

            var eintraege = await kontext.Veranstaltungen.AsNoTracking()
                .Where(v => v.Status == VeranstaltungStatus.Veroeffentlicht
                            && v.Sichtbarkeit == VeranstaltungSichtbarkeit.Oeffentlich
                            && v.Tage.Any(t => !t.Abgesagt && t.Datum >= heute))
                .Select(v => new VeranstaltungKurz(
                    v.Titel,
                    v.Slug,
                    v.Kurzbeschreibung,
                    v.Ort,
                    v.Abteilung != null ? v.Abteilung.Name : null,
                    v.Tage.Where(t => !t.Abgesagt).Min(t => t.Datum),
                    v.Tage.Where(t => !t.Abgesagt).Max(t => t.Datum)))
                .ToListAsync(abbruch);

            return eintraege.OrderBy(e => e.ErsterTag).ThenBy(e => e.Titel).ToList();
        }

        public async Task<VeranstaltungAnzeige?> LadenAsync(string slug, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var v = await kontext.Veranstaltungen.AsNoTracking()
                .Include(x => x.Abteilung)
                .Include(x => x.Tage)
                .SingleOrDefaultAsync(x => x.Slug == slug && Sichtbar.Contains(x.Status), abbruch);
            if (v is null)
                return null;

            // Nur die für die Platzberechnung nötigen Felder, keine Teilnehmerdaten
            var belegungen = await kontext.Anmeldungen.AsNoTracking()
                .Where(a => a.VeranstaltungId == v.Id)
                .Select(a => new AnmeldungBelegung(a.Id, a.Status, a.ReserviertBisUtc, a.AnzahlBegleitpersonen, a.Tage.Select(t => t.VeranstaltungsTagId).ToList()))
                .ToListAsync(abbruch);

            var tage = v.Tage.OrderBy(t => t.Datum).ToList();
            var frei = KapazitaetsRechner.FreiePlaetzeProTag(
                v.Teilnahmemodus,
                tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList(),
                belegungen,
                _zeit.GetUtcNow().UtcDateTime);

            return new VeranstaltungAnzeige(
                v.Titel,
                v.Slug,
                v.Kurzbeschreibung,
                MarkdownText.SicherZuHtml(v.Beschreibung),
                v.Ort,
                v.Adresse,
                v.KartenLink,
                v.Abteilung?.Name,
                v.KontaktName,
                v.KontaktEmail,
                v.Status,
                AnmeldeFenster.Zustand(v, tage, Ortszeit.Jetzt(_zeit)),
                v.AnmeldungAb,
                v.AnmeldungBis,
                v.Teilnahmemodus,
                v.MinTageBeiTeilanmeldung,
                v.MaxBegleitpersonen,
                v.DoubleOptIn,
                v.TelefonFeld,
                v.VereinFeld,
                v.GraduierungFeld,
                v.BemerkungFeld,
                tage.Select(t => new TagAnzeige(t.Id, t.Datum, t.Beginn, t.Ende, t.Titel, t.Abgesagt, t.Abgesagt ? 0 : frei[t.Id])).ToList());
        }
    }
}
