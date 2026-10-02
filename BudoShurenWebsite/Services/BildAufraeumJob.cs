using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services
{
    /// <summary>
    /// Löscht Bilder, die in einem Editor hochgeladen, aber nie gespeichert wurden (Seite ohne Speichern verlassen,
    /// Bild vor dem Speichern wieder entfernt). Läuft über BildAufraeumHostedService.
    /// Gelöscht wird nur, was beides erfüllt: seit <see cref="AufbewahrenFuer"/> vorläufig (DbImage.VorlaeufigSeitUtc)
    /// und nirgends verwendet (BildVerwendung). Alle übrigen Bilder fasst der Job nie an.
    /// </summary>
    public sealed class BildAufraeumJob
    {
        /// <summary>So lange darf ein Editor offen bleiben, ohne dass seine neuen Bilder verschwinden.</summary>
        public static readonly TimeSpan AufbewahrenFuer = TimeSpan.FromDays(2);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public BildAufraeumJob(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        /// <returns>Anzahl der gelöschten Bilder.</returns>
        public async Task<int> AusfuehrenAsync(CancellationToken abbruch)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var grenze = _zeit.GetUtcNow().UtcDateTime - AufbewahrenFuer;

            // Eine einzige Anweisung: Prüfung auf Verwendung und Löschen sind nicht zu trennen. Sonst könnte ein gleichzeitiges
            // Speichern dazwischenkommen, und bei Aktuelles würde das Löschen die Verwendung im Beitrag mitlöschen (Cascade).
            var verwendet = BildVerwendung.VerwendeteBildIds(kontext);
            return await kontext.Images
                .Where(i => i.VorlaeufigSeitUtc != null && i.VorlaeufigSeitUtc < grenze && !verwendet.Contains(i.Id))
                .ExecuteDeleteAsync(abbruch);
        }
    }
}
