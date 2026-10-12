using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services.Kontakt
{
    /// <summary>
    /// Leert im Protokoll des Kontaktformulars Absender und Nachricht, sobald ein Eintrag älter ist als
    /// <see cref="AufbewahrenFuer"/> (so steht es in der Datenschutzerklärung). Zeitpunkt, Abteilung und Ergebnis
    /// bleiben für die Zahlen im Adminbereich. Läuft über KontaktAufraeumHostedService.
    /// </summary>
    public sealed class KontaktAufraeumJob
    {
        /// <summary>So lange bleiben Absender und Nachricht lesbar. Steht auch in der Datenschutzerklärung (Impressum).</summary>
        public static readonly TimeSpan AufbewahrenFuer = TimeSpan.FromDays(90);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public KontaktAufraeumJob(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        /// <returns>Anzahl der geleerten Einträge.</returns>
        public async Task<int> AusfuehrenAsync(CancellationToken abbruch)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var grenze = _zeit.GetUtcNow().UtcDateTime - AufbewahrenFuer;

            return await Abgelaufene(kontext, grenze).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.AbsenderEmail, (string?)null)
                .SetProperty(a => a.Nachricht, (string?)null), abbruch);
        }

        /// <summary>Was der Job leert: vor <paramref name="grenzeUtc"/> eingegangen und noch mit Absender oder Nachricht.</summary>
        public static IQueryable<KontaktAnfrage> Abgelaufene(ApplicationDbContext kontext, DateTime grenzeUtc) =>
            kontext.KontaktAnfragen.Where(a => a.ZeitpunktUtc < grenzeUtc && (a.AbsenderEmail != null || a.Nachricht != null));
    }
}
