using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    public sealed record WartungErgebnis(int VerworfeneAnmeldungen, int AbgeschlosseneVeranstaltungen);

    /// <summary>
    /// Regelmäßige Aufräumarbeiten des Moduls (läuft über VeranstaltungJobsHostedService):
    /// <list type="bullet">
    /// <item>Nie bestätigte Anmeldungen, deren Reservierung seit <see cref="VerwerfenNach"/> abgelaufen ist, werden gelöscht
    /// (Datensparsamkeit). Bis dahin zeigt der Bestätigungslink noch "abgelaufen" statt "ungültig".
    /// War die Anmeldung früher schon einmal bestätigt (erneute Anmeldung nach Abmeldung), bleibt sie mit ihrer Historie als abgemeldet stehen.</item>
    /// <item>Veröffentlichte Veranstaltungen, deren letzter Tag vorbei ist, werden "abgeschlossen".</item>
    /// </list>
    /// Die Anonymisierung nach einer Löschfrist folgt in Phase 2.
    /// </summary>
    public sealed class VeranstaltungWartungJob
    {
        public static readonly TimeSpan VerwerfenNach = TimeSpan.FromDays(7);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public VeranstaltungWartungJob(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        public async Task<WartungErgebnis> AusfuehrenAsync(CancellationToken abbruch)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var jetzt = _zeit.GetUtcNow().UtcDateTime;
            var grenze = jetzt - VerwerfenNach;

            var abgelaufen = await kontext.Anmeldungen
                .Include(a => a.Ereignisse)
                .Where(a => a.Status == AnmeldungStatus.Unbestaetigt && a.ReserviertBisUtc != null && a.ReserviertBisUtc < grenze)
                .ToListAsync(abbruch);

            foreach (var a in abgelaufen)
            {
                if (a.EmailBestaetigtUtc is null)
                {
                    kontext.Anmeldungen.Remove(a);
                }
                else
                {
                    AnmeldungDaten.StatusSetzen(a, AnmeldungStatus.Storniert, EreignisAkteur.System, "Bestätigung nicht rechtzeitig erfolgt");
                    a.ReserviertBisUtc = null;
                    AnmeldungDaten.EreignisHinzufuegen(a, AnmeldungEreignisArt.Storniert, EreignisAkteur.System, jetzt);
                }
            }

            var heute = DateOnly.FromDateTime(Ortszeit.AusUtc(jetzt));
            var vorbei = await kontext.Veranstaltungen
                .Where(v => v.Status == VeranstaltungStatus.Veroeffentlicht && v.Tage.Any() && v.Tage.Max(t => t.Datum) < heute)
                .ToListAsync(abbruch);
            foreach (var v in vorbei)
            {
                v.Status = VeranstaltungStatus.Abgeschlossen;
                v.GeaendertUtc = jetzt;
            }

            await kontext.SaveChangesAsync(abbruch);
            return new WartungErgebnis(abgelaufen.Count, vorbei.Count);
        }
    }
}
