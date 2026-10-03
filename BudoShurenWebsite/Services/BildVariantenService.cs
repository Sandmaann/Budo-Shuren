using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace BudoShurenWebsite.Services
{
    /// <summary>
    /// Verkleinerte Fassungen von Bildern (BildVariante) und die Maße der Originale (DbImage.Breite/Hoehe).
    /// Beides wird beim ersten Bedarf berechnet und in der Datenbank gespeichert, danach nur noch gelesen.
    /// </summary>
    public class BildVariantenService(
        IDbContextFactory<ApplicationDbContext> kontextFabrik,
        TimeProvider zeit,
        ILogger<BildVariantenService> logger)
    {
        public const string ContentType = "image/webp";
        private const int Qualitaet = 75;

        /// <summary>
        /// Maße der angegebenen Bilder. Fehlen sie noch (ältere Bilder), werden sie aus den Bilddaten gelesen und gespeichert.
        /// Bilder, die es nicht gibt oder die sich nicht lesen lassen, fehlen im Ergebnis.
        /// </summary>
        public async Task<Dictionary<int, (int Breite, int Hoehe)>> AbmessungenAsync(IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default)
        {
            var ergebnis = new Dictionary<int, (int Breite, int Hoehe)>();
            if (bildIds.Count == 0)
                return ergebnis;

            await using var kontext = await kontextFabrik.CreateDbContextAsync(abbruch);
            var bekannt = await kontext.Images
                .Where(b => bildIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Breite, b.Hoehe })
                .ToListAsync(abbruch);

            foreach (var bild in bekannt)
            {
                if (bild.Breite is { } breite && bild.Hoehe is { } hoehe)
                    ergebnis[bild.Id] = (breite, hoehe);
                else if (await AbmessungenNachtragenAsync(kontext, bild.Id, abbruch) is { } masse)
                    ergebnis[bild.Id] = masse;
            }
            return ergebnis;
        }

        /// <summary>
        /// Trägt bei Galerie-Einträgen die Maße ihrer Kachel ein, soweit die Maße des Bildes schon gespeichert sind.
        /// Lädt keine Bilddaten: fehlende Maße kommen dazu, sobald die Kachel das erste Mal erzeugt wird.
        /// </summary>
        public async Task KachelMasseSetzenAsync(IReadOnlyCollection<GalerieEintrag> eintraege, CancellationToken abbruch = default)
        {
            var bildIds = eintraege.Where(e => e.DbImageId != null).Select(e => e.DbImageId!.Value).Distinct().ToList();
            if (bildIds.Count == 0)
                return;

            await using var kontext = await kontextFabrik.CreateDbContextAsync(abbruch);
            var bekannt = await kontext.Images
                .Where(b => bildIds.Contains(b.Id) && b.Breite != null && b.Hoehe != null)
                .Select(b => new { b.Id, Breite = b.Breite!.Value, Hoehe = b.Hoehe!.Value })
                .ToDictionaryAsync(b => b.Id, abbruch);

            foreach (var eintrag in eintraege)
            {
                if (eintrag.DbImageId is { } bildId && bekannt.TryGetValue(bildId, out var bild) && bild.Breite > 0 && bild.Hoehe > 0)
                    (eintrag.KachelBreite, eintrag.KachelHoehe) = BildZuschnitt.Zielmasse(BildVariantenArt.GalerieKachel, bild.Breite, bild.Hoehe);
            }
        }

        private async Task<(int Breite, int Hoehe)?> AbmessungenNachtragenAsync(ApplicationDbContext kontext, int bildId, CancellationToken abbruch)
        {
            try
            {
                var daten = await kontext.Images.Where(b => b.Id == bildId).Select(b => b.ImageData).FirstAsync(abbruch);
                var (breite, hoehe) = SichtbareMasse(daten);
                await kontext.Images.Where(b => b.Id == bildId).ExecuteUpdateAsync(
                    s => s.SetProperty(b => b.Breite, breite).SetProperty(b => b.Hoehe, hoehe), abbruch);
                return (breite, hoehe);
            }
            catch (Exception ex) when (ex is ImageFormatException or InvalidOperationException)
            {
                logger.LogWarning(ex, "Maße von Bild {BildId} lassen sich nicht ermitteln", bildId);
                return null;
            }
        }

        /// <summary>
        /// Maße, wie der Browser das Bild zeigt: Fotos vom Handy sind oft gedreht gespeichert (EXIF-Ausrichtung),
        /// dann sind Breite und Höhe vertauscht. Liest nur den Kopf der Datei.
        /// </summary>
        /// <exception cref="ImageFormatException">Die Daten sind kein unterstütztes Bild.</exception>
        public static (int Breite, int Hoehe) SichtbareMasse(byte[] daten)
        {
            var info = Image.Identify(daten);
            var gedreht = info.Metadata.ExifProfile?.TryGetValue(ExifTag.Orientation, out var ausrichtung) == true
                && ausrichtung.Value is >= 5 and <= 8;
            return gedreht ? (info.Height, info.Width) : (info.Width, info.Height);
        }

        /// <summary>
        /// Die verkleinerte Fassung eines Bildes; beim ersten Aufruf wird sie berechnet und gespeichert.
        /// Null, wenn es das Bild nicht gibt oder es sich nicht verarbeiten lässt (dann das Original ausliefern).
        /// </summary>
        public async Task<BildVariante?> VarianteAsync(int bildId, BildVariantenArt art, CancellationToken abbruch = default)
        {
            await using var kontext = await kontextFabrik.CreateDbContextAsync(abbruch);
            var vorhanden = await Lesen(kontext, bildId, art, abbruch);
            if (vorhanden != null)
                return vorhanden;

            var original = await kontext.Images.Where(b => b.Id == bildId).Select(b => b.ImageData).FirstOrDefaultAsync(abbruch);
            if (original == null)
                return null;

            BildVariante variante;
            (int Breite, int Hoehe) masse;
            try
            {
                (variante, masse) = Berechnen(bildId, art, original);
            }
            catch (ImageFormatException ex)
            {
                logger.LogWarning(ex, "Bild {BildId} lässt sich nicht verkleinern", bildId);
                return null;
            }

            kontext.BildVarianten.Add(variante);
            try
            {
                await kontext.SaveChangesAsync(abbruch);
            }
            catch (DbUpdateException)
            {
                // Ein gleichzeitiger Abruf hat dieselbe Fassung schon gespeichert (eindeutiger Index),
                // oder das Bild wurde inzwischen gelöscht
                kontext.ChangeTracker.Clear();
                return await Lesen(kontext, bildId, art, abbruch);
            }

            // Das Original ist ohnehin geladen: seine Maße gleich mit festhalten, falls sie noch fehlen
            await kontext.Images.Where(b => b.Id == bildId && (b.Breite == null || b.Hoehe == null)).ExecuteUpdateAsync(
                s => s.SetProperty(b => b.Breite, masse.Breite).SetProperty(b => b.Hoehe, masse.Hoehe), abbruch);
            return variante;
        }

        private static Task<BildVariante?> Lesen(ApplicationDbContext kontext, int bildId, BildVariantenArt art, CancellationToken abbruch) =>
            kontext.BildVarianten.AsNoTracking().FirstOrDefaultAsync(v => v.BildId == bildId && v.Art == art, abbruch);

        /// <returns>Die Variante und die Maße des (aufgerichteten) Originals.</returns>
        private (BildVariante Variante, (int Breite, int Hoehe) Original) Berechnen(int bildId, BildVariantenArt art, byte[] original)
        {
            using var bild = Image.Load(original);
            bild.Mutate(x => x.AutoOrient()); // gedreht gespeicherte Fotos aufrichten, passend zu SichtbareMasse
            var originalMasse = (bild.Width, bild.Height);
            var (breite, hoehe) = BildZuschnitt.Zielmasse(art, bild.Width, bild.Height);
            // Crop: verkleinert und schneidet mittig auf das feste Seitenverhältnis zu
            bild.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Crop, Size = new Size(breite, hoehe) }));

            using var ausgabe = new MemoryStream();
            bild.Save(ausgabe, new WebpEncoder { Quality = Qualitaet });
            var variante = new BildVariante
            {
                BildId = bildId,
                Art = art,
                Breite = breite,
                Hoehe = hoehe,
                ContentType = ContentType,
                Daten = ausgabe.ToArray(),
                ErstelltUtc = zeit.GetUtcNow().UtcDateTime
            };
            return (variante, originalMasse);
        }
    }
}
