using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BudoShurenWebsite.Services.Entwuerfe
{
    /// <summary>Ein wiederhergestellter Entwurf und wann er zuletzt gesichert wurde.</summary>
    public sealed record GeladenerEntwurf<T>(T Daten, DateTime GeaendertUtc);

    /// <summary>
    /// Ungespeicherte Stände der Bearbeitungsseiten (BearbeitungsEntwurf), je Benutzer und Schlüssel höchstens einer.
    /// Die Seiten nutzen ihn über <see cref="EntwurfSicherung{T}"/>.
    /// </summary>
    public interface IEntwurfSpeicher
    {
        /// <summary>null, wenn es keinen Entwurf gibt, er abgelaufen ist oder nicht mehr zum Formularmodell passt.</summary>
        Task<GeladenerEntwurf<T>?> LadenAsync<T>(string benutzerId, string schluessel, CancellationToken abbruch = default) where T : class;

        /// <param name="daten">Formularmodell als JSON (<see cref="EntwurfSpeicher.AlsJson{T}"/>).</param>
        /// <param name="bildIds">Im Entwurf verwendete Bilder: vorläufige Uploads bleiben so lange erhalten wie der Entwurf.</param>
        Task SpeichernAsync(string benutzerId, string schluessel, string daten, IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default);

        Task LoeschenAsync(string benutzerId, string schluessel, CancellationToken abbruch = default);

        /// <summary>Welche der Bilder es nicht mehr gibt (ein wiederhergestellter Entwurf darf nicht auf sie verweisen).</summary>
        Task<IReadOnlySet<int>> FehlendeBilderAsync(IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default);
    }

    public sealed class EntwurfSpeicher : IEntwurfSpeicher
    {
        /// <summary>
        /// So lange nach der letzten Änderung lässt sich ein Entwurf wiederherstellen.
        /// Gleich lang wie vorläufige Bilder leben: jede Sicherung frischt die Bilder des Entwurfs auf.
        /// </summary>
        public static readonly TimeSpan AufbewahrenFuer = BildAufraeumJob.AufbewahrenFuer;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;
        private readonly ILogger<EntwurfSpeicher> _logger;

        public EntwurfSpeicher(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit, ILogger<EntwurfSpeicher> logger)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
            _logger = logger;
        }

        public static string AlsJson<T>(T daten) => JsonSerializer.Serialize(daten);

        private DateTime JetztUtc => _zeit.GetUtcNow().UtcDateTime;

        public async Task<GeladenerEntwurf<T>?> LadenAsync<T>(string benutzerId, string schluessel, CancellationToken abbruch = default) where T : class
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var grenze = JetztUtc - AufbewahrenFuer;
            var entwurf = await kontext.BearbeitungsEntwuerfe.AsNoTracking()
                .FirstOrDefaultAsync(e => e.BenutzerId == benutzerId && e.Schluessel == schluessel && e.GeaendertUtc >= grenze, abbruch);
            if (entwurf is null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<T>(entwurf.Daten) is { } daten ? new GeladenerEntwurf<T>(daten, entwurf.GeaendertUtc) : null;
            }
            catch (JsonException ex)
            {
                // Z. B. nach einem Update mit geändertem Formularmodell: dann lieber ohne Entwurf weiterarbeiten
                _logger.LogWarning(ex, "Entwurf {Schluessel} lässt sich nicht lesen und wird ignoriert.", schluessel);
                return null;
            }
        }

        public async Task SpeichernAsync(string benutzerId, string schluessel, string daten, IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var jetzt = JetztUtc;

            if (await AktualisierenAsync(kontext, benutzerId, schluessel, daten, jetzt, abbruch) == 0)
            {
                kontext.BearbeitungsEntwuerfe.Add(new BearbeitungsEntwurf { BenutzerId = benutzerId, Schluessel = schluessel, Daten = daten, GeaendertUtc = jetzt });
                try
                {
                    await kontext.SaveChangesAsync(abbruch);
                }
                catch (DbUpdateException)
                {
                    // Dieselbe Seite in zwei Tabs: der andere hat den Entwurf gerade angelegt (eindeutiger Index)
                    kontext.ChangeTracker.Clear();
                    if (await AktualisierenAsync(kontext, benutzerId, schluessel, daten, jetzt, abbruch) == 0)
                        throw;
                }

                // Aufräumen nebenbei, wenn ein neuer Entwurf entsteht: abgelaufene Entwürfe aller Benutzer
                var grenze = jetzt - AufbewahrenFuer;
                await kontext.BearbeitungsEntwuerfe.Where(e => e.GeaendertUtc < grenze).ExecuteDeleteAsync(abbruch);
            }

            if (bildIds.Count > 0)
            {
                // BildAufraeumJob löscht vorläufige Bilder nach AufbewahrenFuer: mit jeder Sicherung beginnt die Frist neu
                await kontext.Images
                    .Where(i => bildIds.Contains(i.Id) && i.VorlaeufigSeitUtc != null)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.VorlaeufigSeitUtc, (DateTime?)jetzt), abbruch);
            }
        }

        private static Task<int> AktualisierenAsync(ApplicationDbContext kontext, string benutzerId, string schluessel, string daten, DateTime jetzt, CancellationToken abbruch) =>
            kontext.BearbeitungsEntwuerfe
                .Where(e => e.BenutzerId == benutzerId && e.Schluessel == schluessel)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Daten, daten).SetProperty(e => e.GeaendertUtc, jetzt), abbruch);

        public async Task LoeschenAsync(string benutzerId, string schluessel, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            await kontext.BearbeitungsEntwuerfe
                .Where(e => e.BenutzerId == benutzerId && e.Schluessel == schluessel)
                .ExecuteDeleteAsync(abbruch);
        }

        public async Task<IReadOnlySet<int>> FehlendeBilderAsync(IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default)
        {
            if (bildIds.Count == 0)
                return new HashSet<int>();

            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var vorhanden = await kontext.Images.Where(i => bildIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(abbruch);
            return bildIds.Except(vorhanden).ToHashSet();
        }
    }
}
