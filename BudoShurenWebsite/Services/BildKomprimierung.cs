using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace BudoShurenWebsite.Services
{
    /// <summary>
    /// Bereitet hochgeladene Bilder für die Datenbank auf: höchstens 1920 × 1080 Pixel, als JPEG,
    /// je größer das Bild, desto stärker komprimiert. Genutzt vom FilesaveController und von den Veranstaltungen.
    /// </summary>
    public static class BildKomprimierung
    {
        public const int MaximaleBreite = 1920;
        public const int MaximaleHoehe = 1080;

        /// <summary>Höchstgröße der hochgeladenen Datei (vor der Komprimierung).</summary>
        public const long MaximaleDateigroesse = 5 * 1024 * 1024;

        /// <exception cref="UnknownImageFormatException">Die Daten sind kein unterstütztes Bild.</exception>
        public static async Task<byte[]> AlsJpegAsync(Stream daten, CancellationToken abbruch = default)
        {
            using var bild = await Image.LoadAsync(daten, abbruch);
            if (bild.Height > MaximaleHoehe || bild.Width > MaximaleBreite)
            {
                bild.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaximaleBreite, MaximaleHoehe)
                }));
            }

            await using var ausgabe = new MemoryStream();
            await bild.SaveAsync(ausgabe, new JpegEncoder { Quality = Qualitaet(bild.Width, bild.Height) }, abbruch);
            return ausgabe.ToArray();
        }

        /// <summary>Je größer das Bild, desto stärker komprimieren.</summary>
        public static int Qualitaet(int breite, int hoehe) => ((long)breite * hoehe) switch
        {
            > 2_000_000 => 70, // z. B. 1920 × 1080
            > 1_000_000 => 75,
            > 500_000 => 85,
            _ => 90
        };
    }
}
