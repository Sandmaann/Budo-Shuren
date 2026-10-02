using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>
    /// Kommende veröffentlichte Veranstaltungen: kein Termin überbucht (die Platzprüfung lässt das nie zu,
    /// ein Befund heißt also Datenfehler) und mindestens ein aktiver Empfänger für die Organisator-Benachrichtigungen.
    /// </summary>
    public sealed class VeranstaltungAnmeldungenCheck : IHealthCheck
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;
        private readonly VeranstaltungenOptionen _optionen;

        public VeranstaltungAnmeldungenCheck(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit, IOptions<VeranstaltungenOptionen> optionen)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
            _optionen = optionen.Value;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!_optionen.Aktiviert)
                return Pruefergebnis.VeranstaltungenAus;

            await using var kontext = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var jetzt = _zeit.GetUtcNow().UtcDateTime;
            var heute = DateOnly.FromDateTime(Ortszeit.AusUtc(jetzt));

            var kommende = await kontext.Veranstaltungen.AsNoTracking()
                .Where(v => v.Status == VeranstaltungStatus.Veroeffentlicht && v.Tage.Any(t => t.Datum >= heute))
                .Include(v => v.Tage)
                .Include(v => v.Anmeldungen).ThenInclude(a => a.Tage)
                .Include(v => v.BenachrichtigungEmpfaenger)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var ueberbucht = new List<string>();
            var ohneEmpfaenger = new List<string>();
            foreach (var v in kommende)
            {
                var tage = v.Tage.Select(t => new TagKapazitaet(t.Id, t.MaxTeilnehmer, t.Abgesagt)).ToList();
                var belegt = KapazitaetsRechner.BelegungProTag(v.Teilnahmemodus, tage, v.Anmeldungen.Select(AnmeldungDaten.Belegung), jetzt);
                foreach (var tag in v.Tage.Where(t => t.MaxTeilnehmer is not null && belegt.GetValueOrDefault(t.Id) > t.MaxTeilnehmer).OrderBy(t => t.Datum).ThenBy(t => t.Beginn))
                    ueberbucht.Add($"{v.Titel} ({tag.Datum:dd.MM.yyyy} {tag.Beginn:HH\\:mm}: {belegt[tag.Id]} von {tag.MaxTeilnehmer})");

                if (!v.BenachrichtigungEmpfaenger.Any(e => e.Modus != BenachrichtigungModus.Pausiert && e.AbgemeldetUtc is null))
                    ohneEmpfaenger.Add(v.Titel);
            }

            var befunde = new List<string>();
            if (ueberbucht.Count > 0)
                befunde.Add($"Überbucht: {Pruefergebnis.Liste(ueberbucht)}.");
            if (ohneEmpfaenger.Count > 0)
                befunde.Add($"Niemand wird über Anmeldungen benachrichtigt: {Pruefergebnis.Liste(ohneEmpfaenger)}.");
            return Pruefergebnis.Aus(befunde, $"{kommende.Count} kommende Veranstaltung(en) ohne Auffälligkeiten.");
        }
    }
}
