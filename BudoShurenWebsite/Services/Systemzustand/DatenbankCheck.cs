using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>Ist die Datenbank erreichbar und sind alle Migrationen angewendet?</summary>
    public sealed class DatenbankCheck : IHealthCheck
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        public DatenbankCheck(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(cancellationToken);
            if (!await kontext.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Keine Verbindung zur Datenbank.");

            var offen = (await kontext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            return offen.Count == 0
                ? HealthCheckResult.Healthy("Erreichbar, alle Migrationen angewendet.")
                : HealthCheckResult.Degraded($"{offen.Count} ausstehende Migration(en): {Pruefergebnis.Liste(offen)}. Anwenden auf der Admin-Seite.");
        }
    }
}
