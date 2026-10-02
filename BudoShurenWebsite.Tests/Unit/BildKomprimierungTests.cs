using BudoShurenWebsite.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace BudoShurenWebsite.Tests.Unit;

[Trait("Category", "Unit")]
public class BildKomprimierungTests
{
    private static MemoryStream Png(int breite, int hoehe)
    {
        using var bild = new Image<Rgba32>(breite, hoehe, new Rgba32(200, 30, 30));
        var daten = new MemoryStream();
        bild.SaveAsPng(daten);
        daten.Position = 0;
        return daten;
    }

    [Fact]
    public async Task Grosse_Bilder_werden_verkleinert_und_als_JPEG_gespeichert()
    {
        var jpeg = await BildKomprimierung.AlsJpegAsync(Png(4000, 1000), TestContext.Current.CancellationToken);

        Image.DetectFormat(jpeg).ShouldBe(JpegFormat.Instance);
        var info = Image.Identify(jpeg);
        (info.Width, info.Height).ShouldBe((1920, 480), "Seitenverhältnis bleibt");
    }

    [Fact]
    public async Task Kleine_Bilder_behalten_ihre_Groesse()
    {
        var info = Image.Identify(await BildKomprimierung.AlsJpegAsync(Png(300, 200), TestContext.Current.CancellationToken));

        (info.Width, info.Height).ShouldBe((300, 200));
    }

    [Fact]
    public async Task Keine_Bilddaten_werden_abgelehnt()
    {
        await Should.ThrowAsync<UnknownImageFormatException>(() =>
            BildKomprimierung.AlsJpegAsync(new MemoryStream("kein Bild"u8.ToArray()), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1920, 1080, 70)]
    [InlineData(1280, 800, 75)]
    [InlineData(800, 700, 85)]
    [InlineData(400, 300, 90)]
    public void Je_groesser_desto_staerker_komprimiert(int breite, int hoehe, int qualitaet)
    {
        BildKomprimierung.Qualitaet(breite, hoehe).ShouldBe(qualitaet);
    }
}
