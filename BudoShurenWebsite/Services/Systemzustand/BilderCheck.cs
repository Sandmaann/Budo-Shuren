using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>
    /// Lose Bilder: Bilddaten, auf die nichts mehr verweist (BildVerwendung). Löscht nichts, meldet nur.
    /// <list type="bullet">
    /// <item>Vorläufige Uploads, die BildAufraeumJob längst hätte löschen müssen: das Aufräumen läuft nicht.</item>
    /// <item>Nicht vorläufige Bilder ohne Verwendung, älter als ein Tag: z. B. abgebrochene Uploads in Galerie,
    /// Neuigkeiten oder Wissen oder ersetzte Bilder. Diese räumt kein Job auf.</item>
    /// </list>
    /// </summary>
    public sealed class BilderCheck : IHealthCheck
    {
        /// <summary>Bilder aus Editoren ohne Markierung sind kurz unverwendet, bis der Inhalt gespeichert ist.</summary>
        public static readonly TimeSpan LoseErstNach = TimeSpan.FromDays(1);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public BilderCheck(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var jetzt = _zeit.GetUtcNow().UtcDateTime;
            var befunde = new List<string>();
            var daten = new Dictionary<string, object>();

            var nichtAufgeraeumt = await BildAufraeumJob
                .Abgelaufene(kontext, jetzt - BildAufraeumJob.AufbewahrenFuer - Pruefergebnis.JobToleranz)
                .CountAsync(cancellationToken);
            daten["vorlaeufigNichtAufgeraeumt"] = nichtAufgeraeumt;
            if (nichtAufgeraeumt > 0)
                befunde.Add($"{nichtAufgeraeumt} nie gespeicherte Upload(s) hätten schon gelöscht sein müssen: läuft das Aufräumen (BildAufraeumen)?");

            // CreatedAt ist je nach Herkunft Orts- oder UTC-Zeit; bei einem Tag Abstand spielt das keine Rolle
            var verwendet = BildVerwendung.VerwendeteBildIds(kontext);
            var lose = kontext.Images.Where(i => i.VorlaeufigSeitUtc == null && i.CreatedAt < jetzt - LoseErstNach && !verwendet.Contains(i.Id));
            var anzahl = await lose.CountAsync(cancellationToken);
            daten["lose"] = anzahl;
            if (anzahl > 0)
            {
                var bytes = await lose.SumAsync(i => (long)i.ImageData.Length, cancellationToken);
                var ids = await lose.OrderBy(i => i.Id).Select(i => i.Id).Take(Pruefergebnis.MaxBeispiele).ToListAsync(cancellationToken);
                daten["loseMegabyte"] = Math.Round(bytes / 1024d / 1024d, 1);
                daten["loseIds"] = ids;
                befunde.Add($"{anzahl} Bild(er) ohne Verwendung ({bytes / 1024d / 1024d:0.0} MB), z. B. abgebrochene Uploads oder ersetzte Bilder. IDs: {Pruefergebnis.Liste(ids.Select(i => i.ToString()))}.");
            }

            return Pruefergebnis.Aus(befunde, "Keine losen Bilder.", daten);
        }
    }
}
