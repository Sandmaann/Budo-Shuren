using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace BudoShurenWebsite.Tests.Unit;

[Trait("Category", "Unit")]
public class BildZuschnittTests
{
    [Theory]
    [InlineData(1920, 1080, 896, 672)]  // Querformat: 4:3
    [InlineData(1000, 1000, 896, 672)]  // quadratisch zählt als Querformat
    [InlineData(608, 1080, 576, 768)]   // Hochformat: 3:4
    [InlineData(1080, 1920, 576, 768)]
    public void Grosse_Bilder_bekommen_das_feste_Format(int breite, int hoehe, int zielBreite, int zielHoehe)
    {
        BildZuschnitt.Zielmasse(BildVariantenArt.Neuigkeit, breite, hoehe).ShouldBe((zielBreite, zielHoehe));
    }

    [Theory]
    [InlineData(448, 400, 448, 336)]    // schmaler als das Ziel: die Breite begrenzt
    [InlineData(2000, 336, 448, 336)]   // flacher als das Ziel: die Höhe begrenzt
    [InlineData(288, 600, 288, 384)]    // Hochformat
    public void Kleine_Bilder_werden_nur_zugeschnitten_nicht_vergroessert(int breite, int hoehe, int zielBreite, int zielHoehe)
    {
        BildZuschnitt.Zielmasse(BildVariantenArt.Neuigkeit, breite, hoehe).ShouldBe((zielBreite, zielHoehe));
    }

    [Theory]
    [InlineData(1434, 1080, 640, 482)]  // Querformat: die Breite begrenzt
    [InlineData(813, 1080, 640, 850)]   // Hochformat
    [InlineData(600, 1920, 300, 960)]   // sehr hoch: die Höhe begrenzt
    [InlineData(400, 300, 400, 300)]    // kleine Bilder bleiben, wie sie sind
    public void Galerie_Kacheln_werden_verkleinert_aber_nicht_zugeschnitten(int breite, int hoehe, int zielBreite, int zielHoehe)
    {
        BildZuschnitt.Zielmasse(BildVariantenArt.GalerieKachel, breite, hoehe).ShouldBe((zielBreite, zielHoehe));
    }

    [Fact]
    public void Winzige_Bilder_ergeben_mindestens_einen_Pixel()
    {
        BildZuschnitt.Zielmasse(BildVariantenArt.Neuigkeit, 1, 1).ShouldBe((1, 1));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    public void Ungueltige_Masse_werden_abgelehnt(int breite, int hoehe)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => BildZuschnitt.Zielmasse(BildVariantenArt.Neuigkeit, breite, hoehe));
    }

    [Theory]
    [InlineData(null, 300, 200)]
    [InlineData((ushort)1, 300, 200)]
    [InlineData((ushort)3, 300, 200)]   // auf dem Kopf: Maße bleiben
    [InlineData((ushort)6, 200, 300)]   // um 90° gedreht gespeichert: der Browser zeigt es hochkant
    [InlineData((ushort)8, 200, 300)]
    public void Sichtbare_Masse_beruecksichtigen_die_EXIF_Ausrichtung(ushort? ausrichtung, int breite, int hoehe)
    {
        using var bild = new Image<Rgba32>(300, 200);
        if (ausrichtung is { } wert)
        {
            bild.Metadata.ExifProfile = new ExifProfile();
            bild.Metadata.ExifProfile.SetValue(ExifTag.Orientation, wert);
        }
        using var daten = new MemoryStream();
        bild.SaveAsJpeg(daten);

        BildVariantenService.SichtbareMasse(daten.ToArray()).ShouldBe((breite, hoehe));
    }
}
