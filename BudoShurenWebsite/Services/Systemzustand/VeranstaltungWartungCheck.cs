using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>
    /// Liegt Arbeit des VeranstaltungWartungJob deutlich länger als nötig? Dieselben Abfragen wie der Job,
    /// nur mit <see cref="Pruefergebnis.JobToleranz"/> Spielraum.
    /// </summary>
    public sealed class VeranstaltungWartungCheck : IHealthCheck
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;
        private readonly VeranstaltungenOptionen _optionen;

        public VeranstaltungWartungCheck(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit, IOptions<VeranstaltungenOptionen> optionen)
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
            var stichtag = _zeit.GetUtcNow().UtcDateTime - Pruefergebnis.JobToleranz;
            var befunde = new List<string>();

            var anmeldungen = await VeranstaltungWartungJob
                .AbgelaufeneAnmeldungen(kontext, stichtag - VeranstaltungWartungJob.VerwerfenNach)
                .CountAsync(cancellationToken);
            if (anmeldungen > 0)
                befunde.Add($"{anmeldungen} unbestätigte Anmeldung(en) hätten schon verworfen sein müssen.");

            var vorbei = await VeranstaltungWartungJob
                .VorbeiAberNichtAbgeschlossen(kontext, DateOnly.FromDateTime(Ortszeit.AusUtc(stichtag)))
                .Select(v => v.Titel)
                .ToListAsync(cancellationToken);
            if (vorbei.Count > 0)
                befunde.Add($"Vorbei, aber nicht abgeschlossen: {Pruefergebnis.Liste(vorbei)}.");

            if (befunde.Count > 0)
                befunde.Add("Läuft die Wartung (Veranstaltungen:HintergrundJobsAktiviert)?");
            return Pruefergebnis.Aus(befunde, "Keine liegengebliebene Wartung.");
        }
    }
}
