using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>
    /// Kommen die Mails raus? Fällige Mails, die lange liegen bleiben, sind "ungesund" (Versand hängt oder ist aus).
    /// "Eingeschränkt": Mails, die nach einem Fehler erneut versucht werden (z. B. SMTP nicht erreichbar; der Versand
    /// verschiebt sie selbst nach hinten, sie gelten deshalb nicht als liegengeblieben), und endgültig fehlgeschlagene
    /// Mails der letzten Tage. Adressen und Fehlertexte stehen nicht in der Antwort.
    /// </summary>
    public sealed class MailWarteschlangeCheck : IHealthCheck
    {
        /// <summary>Normal ist eine Mail nach Sekunden raus; nach Fehlern verschiebt der Versand FaelligAbUtc selbst nach hinten.</summary>
        public static readonly TimeSpan StauNach = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan FehlschlaegeDerLetzten = TimeSpan.FromDays(7);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public MailWarteschlangeCheck(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var jetzt = _zeit.GetUtcNow().UtcDateTime;
            var stauGrenze = jetzt - StauNach;
            var fehlerGrenze = jetzt - FehlschlaegeDerLetzten;

            var wartend = await kontext.EmailAusgang.CountAsync(m => m.Status == EmailStatus.Wartend, cancellationToken);
            var ueberfaellig = await kontext.EmailAusgang.CountAsync(m => m.Status == EmailStatus.Wartend && m.FaelligAbUtc < stauGrenze, cancellationToken);
            var wiederholung = await kontext.EmailAusgang.CountAsync(m => m.Status == EmailStatus.Wartend && m.Versuche > 0, cancellationToken);
            var fehlgeschlagen = await kontext.EmailAusgang.CountAsync(m => m.Status == EmailStatus.Fehlgeschlagen && m.ErstelltUtc >= fehlerGrenze, cancellationToken);
            var daten = new Dictionary<string, object>
            {
                ["wartend"] = wartend,
                ["ueberfaellig"] = ueberfaellig,
                ["wiederholung"] = wiederholung,
                ["fehlgeschlagen"] = fehlgeschlagen
            };

            if (ueberfaellig > 0)
                return HealthCheckResult.Unhealthy($"{ueberfaellig} Mail(s) warten seit über {StauNach.TotalMinutes:0} Minuten auf den Versand.", data: daten);
            var befunde = new List<string>();
            if (wiederholung > 0)
                befunde.Add($"{wiederholung} Mail(s) werden nach einem Fehler erneut versucht (SMTP erreichbar?).");
            if (fehlgeschlagen > 0)
                befunde.Add($"{fehlgeschlagen} Mail(s) in den letzten {FehlschlaegeDerLetzten.TotalDays:0} Tagen endgültig fehlgeschlagen.");
            if (befunde.Count > 0)
                return HealthCheckResult.Degraded(string.Join(" ", befunde) + " Details: Tabelle EmailAusgang, Spalte LetzterFehler.", data: daten);
            return HealthCheckResult.Healthy($"{wartend} Mail(s) in der Warteschlange.", daten);
        }
    }
}
