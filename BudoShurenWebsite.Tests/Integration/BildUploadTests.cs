using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

/// <summary>Upload der Bearbeitungsseiten (BildAuswahl) und das Speichern einer Neuigkeit mit hochgeladenem Bild.</summary>
[Trait("Category", "Integration")]
public class BildUploadTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTime Jetzt = new(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(Jetzt));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private BildUpload Upload => new(new TestKontextFabrik(Datenbank), _zeit);

    private static MemoryStream Png()
    {
        using var bild = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(40, 30);
        var daten = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(bild, daten);
        daten.Position = 0;
        return daten;
    }

    [DatenbankFact]
    public async Task Bild_wird_als_Jpeg_abgelegt_vorlaeufig_nur_auf_Wunsch()
    {
        var vorlaeufig = (await Upload.HochladenAsync(Png(), "fotos/dojo.png", vorlaeufig: true, Abbruch)).Bild.ShouldNotBeNull();
        var fest = (await Upload.HochladenAsync(Png(), "galerie.png", vorlaeufig: false, Abbruch)).Bild.ShouldNotBeNull();

        vorlaeufig.Dateiname.ShouldBe("dojo.png");
        await using var kontext = Datenbank.NeuerKontext();
        var bilder = await kontext.Images.ToDictionaryAsync(i => i.Id, Abbruch);
        bilder[vorlaeufig.Id].VorlaeufigSeitUtc.ShouldBe(Jetzt);
        bilder[vorlaeufig.Id].ContentType.ShouldBe("image/jpeg");
        bilder[vorlaeufig.Id].Title.ShouldBe("dojo.png");
        bilder[fest.Id].VorlaeufigSeitUtc.ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Was_kein_Bild_ist_wird_mit_Meldung_abgelehnt()
    {
        var ergebnis = await Upload.HochladenAsync(new MemoryStream([1, 2, 3, 4]), "notiz.txt", vorlaeufig: true, Abbruch);

        ergebnis.Bild.ShouldBeNull();
        ergebnis.Fehler.ShouldNotBeNull().ShouldContain("notiz.txt");
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.Images.CountAsync(Abbruch)).ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Verwerfen_loescht_nur_unverwendete_Bilder()
    {
        var frei = (await Upload.HochladenAsync(Png(), "frei.png", true, Abbruch)).Bild!.Id;
        var verwendet = (await Upload.HochladenAsync(Png(), "verwendet.png", true, Abbruch)).Bild!.Id;
        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.Galerie.Add(new GalerieEintrag { Titel = "Eintrag", DbImageId = verwendet });
            await kontext.SaveChangesAsync(Abbruch);
        }

        await Upload.VerwerfenAsync([frei, verwendet], Abbruch);

        await using (var kontext = Datenbank.NeuerKontext())
            (await kontext.Images.Select(i => i.Id).ToListAsync(Abbruch)).ShouldBe([verwendet]);
    }

    [DatenbankFact]
    public async Task Neue_Neuigkeit_braucht_ein_Bild_und_uebernimmt_es()
    {
        var eingabe = new NeuigkeitEingabe { Titel = "Lehrgang", Beschreibung = "Text", IstStandardneuigkeit = true, Ablaufdatum = new DateTime(2027, 1, 1) };

        await using (var kontext = Datenbank.NeuerKontext())
            (await eingabe.SpeichernAsync(kontext, "olga", Abbruch)).ShouldBe("Bitte ein Bild hochladen.");

        eingabe.DbImageId = 999_999;
        await using (var kontext = Datenbank.NeuerKontext())
            (await eingabe.SpeichernAsync(kontext, "olga", Abbruch)).ShouldNotBeNull().ShouldContain("gibt es nicht mehr");

        eingabe.DbImageId = (await Upload.HochladenAsync(Png(), "lehrgang.png", true, Abbruch)).Bild!.Id;
        await using (var kontext = Datenbank.NeuerKontext())
            (await eingabe.SpeichernAsync(kontext, "olga", Abbruch)).ShouldBeNull();

        await using (var kontext = Datenbank.NeuerKontext())
        {
            var neuigkeit = await kontext.Neuigkeiten.Include(n => n.DbImage).SingleAsync(Abbruch);
            neuigkeit.Titel.ShouldBe("Lehrgang");
            neuigkeit.EntryCreatedBy.ShouldBe("olga");
            neuigkeit.Ablaufdatum.ShouldBeNull("Standardneuigkeiten laufen nicht ab");
            neuigkeit.DbImage!.VorlaeufigSeitUtc.ShouldBeNull("mit dem Speichern ist das Bild übernommen");
        }
    }

    [DatenbankFact]
    public async Task Bestehende_Neuigkeit_wird_geaendert_das_Bild_bleibt()
    {
        int id, bildId;
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var neuigkeit = new Neuigkeit { Titel = "Alt", Beschreibung = "Alt", EntryCreatedBy = "paul", DbImage = new DbImage { Title = "b", ImageData = [1], ContentType = "image/jpeg" } };
            kontext.Neuigkeiten.Add(neuigkeit);
            await kontext.SaveChangesAsync(Abbruch);
            (id, bildId) = (neuigkeit.ID, neuigkeit.DbImageId!.Value);
        }

        NeuigkeitEingabe eingabe;
        await using (var kontext = Datenbank.NeuerKontext())
            eingabe = NeuigkeitEingabe.Aus(await kontext.Neuigkeiten.SingleAsync(Abbruch));
        eingabe.Id.ShouldBe(id);
        eingabe.Titel = "Neu";
        eingabe.Ort = "Augsburg";
        eingabe.Ablaufdatum = new DateTime(2027, 1, 1);

        await using (var kontext = Datenbank.NeuerKontext())
            (await eingabe.SpeichernAsync(kontext, "olga", Abbruch)).ShouldBeNull();

        await using (var kontext = Datenbank.NeuerKontext())
        {
            var neuigkeit = await kontext.Neuigkeiten.SingleAsync(Abbruch);
            (neuigkeit.Titel, neuigkeit.Ort, neuigkeit.Ablaufdatum).ShouldBe(("Neu", "Augsburg", new DateTime(2027, 1, 1)));
            (neuigkeit.EntryCreatedBy, neuigkeit.LastChangedBy, neuigkeit.DbImageId).ShouldBe(("paul", "olga", bildId));
        }

        eingabe.Id = id + 1000;
        await using (var kontext = Datenbank.NeuerKontext())
            (await eingabe.SpeichernAsync(kontext, "olga", Abbruch)).ShouldBe("Die Neuigkeit gibt es nicht mehr.");
    }
}
