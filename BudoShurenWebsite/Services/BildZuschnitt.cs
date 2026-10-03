using BudoShurenWebsite.Models;

namespace BudoShurenWebsite.Services
{
    /// <summary>
    /// Maße für verkleinerte Bilder (ohne Datenbank).
    /// Neuigkeiten: festes Format, Querformat 4:3, Hochformat 3:4. Die Maße sind das Doppelte der Anzeigegröße,
    /// damit das Bild auch auf hochauflösenden Displays scharf ist; die Anzeigegröße steht in den Stilen von
    /// Home/Neuigkeiten.razor (.neuigkeit-bild).
    /// Galerie-Kacheln: Seitenverhältnis bleibt, nur verkleinert.
    /// </summary>
    public static class BildZuschnitt
    {
        public const int NeuigkeitQuerBreite = 896;
        public const int NeuigkeitQuerHoehe = 672;
        public const int NeuigkeitHochBreite = 576;
        public const int NeuigkeitHochHoehe = 768;

        // Galerie-Kacheln sind höchstens gut 600 Pixel breit (kleine Galerie, einspaltig)
        public const int GalerieKachelMaxBreite = 640;
        public const int GalerieKachelMaxHoehe = 960;

        public static bool IstHochkant(int breite, int hoehe) => hoehe > breite;

        /// <summary>
        /// Zielmaße für ein Bild der angegebenen Größe, nie größer als das Original
        /// (kleine Bilder werden höchstens zugeschnitten, nicht vergrößert).
        /// </summary>
        public static (int Breite, int Hoehe) Zielmasse(BildVariantenArt art, int breite, int hoehe)
        {
            if (breite <= 0 || hoehe <= 0)
                throw new ArgumentOutOfRangeException(nameof(breite), "Breite und Höhe müssen größer als 0 sein.");

            if (art == BildVariantenArt.GalerieKachel)
            {
                // Seitenverhältnis bleibt: nur so weit verkleinern, dass das Bild in den Rahmen passt
                var verkleinerung = Math.Min(1d, Math.Min((double)GalerieKachelMaxBreite / breite, (double)GalerieKachelMaxHoehe / hoehe));
                return (Math.Max(1, (int)Math.Round(breite * verkleinerung)), Math.Max(1, (int)Math.Round(hoehe * verkleinerung)));
            }

            var (maxBreite, maxHoehe) = art switch
            {
                BildVariantenArt.Neuigkeit => IstHochkant(breite, hoehe)
                    ? (NeuigkeitHochBreite, NeuigkeitHochHoehe)
                    : (NeuigkeitQuerBreite, NeuigkeitQuerHoehe),
                _ => throw new ArgumentOutOfRangeException(nameof(art), art, null)
            };

            // Größter Ausschnitt im Zielverhältnis, der ins Original passt
            var faktor = Math.Min(1d, Math.Min((double)breite / maxBreite, (double)hoehe / maxHoehe));
            return (Math.Max(1, (int)Math.Round(maxBreite * faktor)), Math.Max(1, (int)Math.Round(maxHoehe * faktor)));
        }
    }
}
