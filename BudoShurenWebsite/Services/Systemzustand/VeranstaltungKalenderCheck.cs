using BudoShurenWebsite.Data;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>
    /// Stimmen die Kalendereinträge mit den Veranstaltungen überein? Maßstab ist dieselbe Regel wie beim Abgleich
    /// (KalenderEintragFabrik.GehoertInDenKalender). Abweichungen behebt erneutes Speichern der Veranstaltung.
    /// </summary>
    public sealed class VeranstaltungKalenderCheck : IHealthCheck
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly VeranstaltungenOptionen _optionen;

        public VeranstaltungKalenderCheck(IDbContextFactory<ApplicationDbContext> dbFactory, IOptions<VeranstaltungenOptionen> optionen)
        {
            _dbFactory = dbFactory;
            _optionen = optionen.Value;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!_optionen.Aktiviert)
                return Pruefergebnis.VeranstaltungenAus;

            await using var kontext = await _dbFactory.CreateDbContextAsync(cancellationToken);
            // Wenige Veranstaltungen pro Jahr: komplett laden und die Regel in C# anwenden, statt sie in SQL nachzubauen
            var veranstaltungen = await kontext.Veranstaltungen.AsNoTracking().Include(v => v.Tage).ToListAsync(cancellationToken);
            var eintraegeProTag = (await kontext.Appointments.AsNoTracking()
                    .Where(a => a.VeranstaltungsTagId != null)
                    .Select(a => a.VeranstaltungsTagId!.Value)
                    .ToListAsync(cancellationToken))
                .GroupBy(id => id)
                .ToDictionary(g => g.Key, g => g.Count());

            var fehlend = new List<string>();
            var zuviel = new List<string>();
            foreach (var v in veranstaltungen)
            {
                foreach (var tag in v.Tage.OrderBy(t => t.Datum).ThenBy(t => t.Beginn))
                {
                    var anzahl = eintraegeProTag.GetValueOrDefault(tag.Id);
                    var soll = KalenderEintragFabrik.GehoertInDenKalender(v, tag) ? 1 : 0;
                    var bezeichnung = $"{v.Titel} ({tag.Datum:dd.MM.yyyy} {tag.Beginn:HH\\:mm})";
                    if (anzahl < soll)
                        fehlend.Add(bezeichnung);
                    else if (anzahl > soll)
                        zuviel.Add(bezeichnung);
                }
            }

            var befunde = new List<string>();
            if (fehlend.Count > 0)
                befunde.Add($"Fehlt im Kalender: {Pruefergebnis.Liste(fehlend)}.");
            if (zuviel.Count > 0)
                befunde.Add($"Im Kalender, obwohl nicht (oder mehrfach) vorgesehen: {Pruefergebnis.Liste(zuviel)}.");
            if (befunde.Count > 0)
                befunde.Add("Erneutes Speichern der Veranstaltung gleicht den Kalender ab.");
            return Pruefergebnis.Aus(befunde, "Kalender stimmt mit den Veranstaltungen überein.");
        }
    }
}
