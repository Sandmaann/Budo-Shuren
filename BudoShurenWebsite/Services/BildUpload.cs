using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services
{
    /// <summary>Ein über die Bearbeitungsseiten hochgeladenes Bild (Components/Shared/Controls/BildAuswahl).</summary>
    public sealed record HochgeladenesBild(int Id, string Dateiname);

    /// <summary>Ergebnis eines Uploads: entweder das Bild oder eine Meldung für den Benutzer.</summary>
    public sealed record BildUploadErgebnis(HochgeladenesBild? Bild, string? Fehler);

    /// <summary>
    /// Nimmt Bilder der Bearbeitungsseiten entgegen: verkleinert sie (BildKomprimierung) und legt sie als DbImage ab.
    /// Läuft über die Verbindung der Seite, nicht über eine eigene Anfrage des Browsers: so kann kein Upload ankommen,
    /// dessen Ergebnis die Seite nicht mehr erfährt.
    /// </summary>
    public interface IBildUpload
    {
        /// <param name="vorlaeufig">Bis zum Speichern des Inhalts vorläufig: nie gespeicherte Bilder räumt BildAufraeumJob auf.</param>
        Task<BildUploadErgebnis> HochladenAsync(Stream daten, string dateiname, bool vorlaeufig, CancellationToken abbruch = default);

        /// <summary>Löscht hochgeladene Bilder wieder, soweit sie nirgends verwendet werden.</summary>
        Task VerwerfenAsync(IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default);
    }

    public sealed class BildUpload : IBildUpload
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;

        public BildUpload(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
        }

        public async Task<BildUploadErgebnis> HochladenAsync(Stream daten, string dateiname, bool vorlaeufig, CancellationToken abbruch = default)
        {
            var name = Path.GetFileName(dateiname);
            byte[] jpeg;
            try
            {
                jpeg = await BildKomprimierung.AlsJpegAsync(daten, abbruch);
            }
            catch (Exception ex) when (ex is SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
            {
                return new BildUploadErgebnis(null, $"„{name}“ ist kein unterstütztes Bild (JPG, PNG oder WebP).");
            }

            var jetzt = _zeit.GetUtcNow().UtcDateTime;
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var bild = new DbImage { Title = name, ImageData = jpeg, ContentType = "image/jpeg", CreatedAt = jetzt, VorlaeufigSeitUtc = vorlaeufig ? jetzt : null };
            kontext.Images.Add(bild);
            await kontext.SaveChangesAsync(abbruch);
            return new BildUploadErgebnis(new HochgeladenesBild(bild.Id, name), null);
        }

        public async Task VerwerfenAsync(IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            await BildVerwendung.UnverwendeteLoeschenAsync(kontext, bildIds, abbruch);
        }
    }
}
