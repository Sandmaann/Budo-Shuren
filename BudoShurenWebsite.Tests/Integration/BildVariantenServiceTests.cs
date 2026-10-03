using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace BudoShurenWebsite.Tests.Integration;

[Trait("Category", "Integration")]
public class BildVariantenServiceTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTimeOffset Jetzt = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private BildVariantenService Dienst() =>
        new(new TestKontextFabrik(Datenbank), new FakeTimeProvider(Jetzt), NullLogger<BildVariantenService>.Instance);

    private static byte[] Jpeg(int breite, int hoehe, ushort? ausrichtung = null)
    {
        using var bild = new Image<Rgba32>(breite, hoehe, new Rgba32(30, 120, 60));
        if (ausrichtung is { } wert)
        {
            bild.Metadata.ExifProfile = new ExifProfile();
            bild.Metadata.ExifProfile.SetValue(ExifTag.Orientation, wert);
        }
        using var daten = new MemoryStream();
        bild.SaveAsJpeg(daten);
        return daten.ToArray();
    }

    private async Task<int> BildAsync(byte[] daten)
    {
        await using var kontext = Datenbank.NeuerKontext();
        var bild = new DbImage { Title = "bild.jpg", ImageData = daten, ContentType = "image/jpeg", CreatedAt = Jetzt.UtcDateTime };
        kontext.Images.Add(bild);
        await kontext.SaveChangesAsync(Abbruch);
        return bild.Id;
    }

    [DatenbankFact]
    public async Task Variante_wird_zugeschnitten_als_WebP_gespeichert()
    {
        var bildId = await BildAsync(Jpeg(1920, 1080));

        var variante = (await Dienst().VarianteAsync(bildId, BildVariantenArt.Neuigkeit, Abbruch)).ShouldNotBeNull();

        (variante.Breite, variante.Hoehe).ShouldBe((896, 672));
        variante.ContentType.ShouldBe("image/webp");
        variante.ErstelltUtc.ShouldBe(Jetzt.UtcDateTime);
        Image.DetectFormat(variante.Daten).ShouldBe(WebpFormat.Instance);
        var info = Image.Identify(variante.Daten);
        (info.Width, info.Height).ShouldBe((896, 672));

        await using var kontext = Datenbank.NeuerKontext();
        var gespeichert = await kontext.BildVarianten.SingleAsync(Abbruch);
        gespeichert.BildId.ShouldBe(bildId);
        gespeichert.Daten.ShouldBe(variante.Daten);
    }

    [DatenbankFact]
    public async Task Je_Art_gibt_es_eine_eigene_Variante()
    {
        var bildId = await BildAsync(Jpeg(1434, 1080));
        var dienst = Dienst();

        var neuigkeit = (await dienst.VarianteAsync(bildId, BildVariantenArt.Neuigkeit, Abbruch)).ShouldNotBeNull();
        var kachel = (await dienst.VarianteAsync(bildId, BildVariantenArt.GalerieKachel, Abbruch)).ShouldNotBeNull();

        (neuigkeit.Breite, neuigkeit.Hoehe).ShouldBe((896, 672));
        (kachel.Breite, kachel.Hoehe).ShouldBe((640, 482));
        var info = Image.Identify(kachel.Daten);
        (info.Width, info.Height).ShouldBe((640, 482));
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.BildVarianten.CountAsync(Abbruch)).ShouldBe(2);
    }

    [DatenbankFact]
    public async Task Kachelmasse_sind_bekannt_sobald_die_Kachel_einmal_erzeugt_wurde()
    {
        var bildId = await BildAsync(Jpeg(1434, 1080));
        var ohneBild = new GalerieEintrag { Titel = "ohne Bild" };
        var dienst = Dienst();

        // Vor dem ersten Abruf sind die Maße des Bildes noch nicht gespeichert
        var vorher = new GalerieEintrag { DbImageId = bildId };
        await dienst.KachelMasseSetzenAsync([vorher, ohneBild], Abbruch);
        (vorher.KachelBreite, vorher.KachelHoehe).ShouldBe((null, null));

        await dienst.VarianteAsync(bildId, BildVariantenArt.GalerieKachel, Abbruch);

        var nachher = new GalerieEintrag { DbImageId = bildId };
        await dienst.KachelMasseSetzenAsync([nachher, ohneBild], Abbruch);
        (nachher.KachelBreite, nachher.KachelHoehe).ShouldBe((640, 482));
        (ohneBild.KachelBreite, ohneBild.KachelHoehe).ShouldBe((null, null));
        await using var kontext = Datenbank.NeuerKontext();
        var bild = await kontext.Images.SingleAsync(Abbruch);
        (bild.Breite, bild.Hoehe).ShouldBe((1434, 1080));
    }

    [DatenbankFact]
    public async Task Variante_wird_nur_einmal_berechnet()
    {
        var bildId = await BildAsync(Jpeg(1200, 900));
        var dienst = Dienst();

        var erste = (await dienst.VarianteAsync(bildId, BildVariantenArt.Neuigkeit, Abbruch)).ShouldNotBeNull();
        var zweite = (await dienst.VarianteAsync(bildId, BildVariantenArt.Neuigkeit, Abbruch)).ShouldNotBeNull();

        zweite.Id.ShouldBe(erste.Id);
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.BildVarianten.CountAsync(Abbruch)).ShouldBe(1);
    }

    [DatenbankFact]
    public async Task Gleichzeitige_Abrufe_speichern_die_Variante_nur_einmal()
    {
        var bildId = await BildAsync(Jpeg(1200, 900));
        var dienst = Dienst();

        var ergebnisse = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => dienst.VarianteAsync(bildId, BildVariantenArt.Neuigkeit, Abbruch)));

        ergebnisse.ShouldAllBe(v => v != null);
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.BildVarianten.CountAsync(Abbruch)).ShouldBe(1);
    }

    [DatenbankFact]
    public async Task Gedreht_gespeichertes_Foto_wird_aufgerichtet_und_hochkant_zugeschnitten()
    {
        var bildId = await BildAsync(Jpeg(1920, 1080, ausrichtung: 6));

        var variante = (await Dienst().VarianteAsync(bildId, BildVariantenArt.Neuigkeit, Abbruch)).ShouldNotBeNull();

        (variante.Breite, variante.Hoehe).ShouldBe((576, 768));
        var info = Image.Identify(variante.Daten);
        (info.Width, info.Height).ShouldBe((576, 768));
    }

    [DatenbankFact]
    public async Task Ohne_Bild_oder_bei_unlesbaren_Daten_gibt_es_keine_Variante()
    {
        var kaputt = await BildAsync([1, 2, 3]);
        var dienst = Dienst();

        (await dienst.VarianteAsync(kaputt, BildVariantenArt.Neuigkeit, Abbruch)).ShouldBeNull();
        (await dienst.VarianteAsync(4711, BildVariantenArt.Neuigkeit, Abbruch)).ShouldBeNull();

        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.BildVarianten.AnyAsync(Abbruch)).ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Variante_wird_mit_dem_Bild_geloescht()
    {
        var bildId = await BildAsync(Jpeg(1200, 900));
        await Dienst().VarianteAsync(bildId, BildVariantenArt.Neuigkeit, Abbruch);

        await using var kontext = Datenbank.NeuerKontext();
        kontext.Images.Remove(await kontext.Images.SingleAsync(b => b.Id == bildId, Abbruch));
        await kontext.SaveChangesAsync(Abbruch);

        (await kontext.BildVarianten.AnyAsync(Abbruch)).ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Abmessungen_werden_nachgetragen_und_gespeichert()
    {
        var quer = await BildAsync(Jpeg(1200, 900));
        var gedreht = await BildAsync(Jpeg(1200, 900, ausrichtung: 6));
        var kaputt = await BildAsync([1, 2, 3]);

        var masse = await Dienst().AbmessungenAsync([quer, gedreht, kaputt, 4711], Abbruch);

        masse.ShouldBe(new Dictionary<int, (int, int)> { [quer] = (1200, 900), [gedreht] = (900, 1200) }, ignoreOrder: true);
        await using var kontext = Datenbank.NeuerKontext();
        var gespeichert = await kontext.Images.ToDictionaryAsync(b => b.Id, b => (b.Breite, b.Hoehe), Abbruch);
        gespeichert[quer].ShouldBe((1200, 900));
        gespeichert[gedreht].ShouldBe((900, 1200));
        gespeichert[kaputt].ShouldBe((null, null));
    }
}
