using BudoShurenWebsite.Data;
using BudoShurenWebsite.Services.Kontakt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>
    /// Protokoll des Kontaktformulars: Einträge, deren Absender und Nachricht KontaktAufraeumJob längst hätte
    /// löschen müssen (die Frist steht in der Datenschutzerklärung). Löscht nichts, meldet nur.
    /// </summary>
    public sealed class KontaktProtokollCheck : IHealthCheck
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public KontaktProtokollCheck(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var grenze = _zeit.GetUtcNow().UtcDateTime - KontaktAufraeumJob.AufbewahrenFuer - Pruefergebnis.JobToleranz;
            var befunde = new List<string>();

            var nichtGeleert = await KontaktAufraeumJob.Abgelaufene(kontext, grenze).CountAsync(cancellationToken);
            if (nichtGeleert > 0)
                befunde.Add($"Bei {nichtGeleert} Nachricht(en) aus dem Kontaktformular hätten Absender und Text schon gelöscht sein müssen: läuft das Aufräumen (Kontaktformular)?");

            return Pruefergebnis.Aus(befunde, "Keine Nachrichten über der Aufbewahrungsfrist.", new Dictionary<string, object> { ["nichtGeleert"] = nichtGeleert });
        }
    }
}
