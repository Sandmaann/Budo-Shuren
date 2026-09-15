using System.Diagnostics;
using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services
{
    public sealed class AdminMaintenanceService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IHostApplicationLifetime applicationLifetime,
        ILogger<AdminMaintenanceService> logger)
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory = dbContextFactory;
        private readonly IHostApplicationLifetime _applicationLifetime = applicationLifetime;
        private readonly ILogger<AdminMaintenanceService> _logger = logger;

        public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        }

        public async Task<AdminActionResult> ApplyMigrationsAsync(string initiatedBy, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

                if (pendingMigrations.Length == 0)
                {
                    _logger.LogInformation("Keine ausstehenden Migrationen. Angefordert von {InitiatedBy}", initiatedBy);
                    return new AdminActionResult(true, "Es sind keine ausstehenden Migrationen vorhanden.", 0);
                }

                var watch = Stopwatch.StartNew();
                await dbContext.Database.MigrateAsync(cancellationToken);
                watch.Stop();

                _logger.LogInformation(
                    "Migration erfolgreich. {Count} Migration(en) durch {InitiatedBy} in {ElapsedMs} ms ausgeführt.",
                    pendingMigrations.Length,
                    initiatedBy,
                    watch.ElapsedMilliseconds);

                return new AdminActionResult(
                    true,
                    $"Migration erfolgreich ausgeführt ({pendingMigrations.Length} Migration(en), {watch.ElapsedMilliseconds} ms).",
                    watch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei manueller Migration durch {InitiatedBy}", initiatedBy);
                return new AdminActionResult(false, $"Migration fehlgeschlagen: {ex.GetBaseException().Message}", 0);
            }
        }

        public Task StopApplicationAsync(string initiatedBy, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Anwendungsstopp durch {InitiatedBy} angefordert.", initiatedBy);
            _applicationLifetime.StopApplication();
            return Task.CompletedTask;
        }
    }

    public sealed record AdminActionResult(bool Success, string Message, long DurationMs);
}