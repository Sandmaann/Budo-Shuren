using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Tests.Integration;

[Trait("Category", "Integration")]
public class AktuellesServiceTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    /// <summary>Wie ein Upload aus dem Editor (FilesaveController mit vorlaeufig=true).</summary>
    private async Task<int> HochladenAsync(string titel)
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await new ImageService(kontext).UploadImageAsync(titel, [1, 2, 3], "image/jpeg", vorlaeufig: true);
    }

    private async Task<Dictionary<string, bool>> VorlaeufigAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Images.ToDictionaryAsync(i => i.Title, i => i.VorlaeufigSeitUtc != null, Abbruch);
    }

    [DatenbankFact]
    public async Task Speichern_uebernimmt_nur_die_Bilder_im_Beitrag()
    {
        var verwendet = await HochladenAsync("verwendet");
        var spaeter = await HochladenAsync("spaeter");
        await HochladenAsync("wieder entfernt");
        (await VorlaeufigAsync()).Values.ShouldAllBe(v => v);

        // Wie die Bearbeiten-Seite: ein Kontext für Anlegen und spätere Änderungen
        await using var kontext = Datenbank.NeuerKontext();
        var service = new AktuellesService(kontext);
        var beitrag = new AktuellesBeitrag
        {
            Titel = "Lehrgang",
            Slug = "lehrgang",
            Bloecke = [new AktuellesBlock { Typ = AktuellesBlockTyp.BilderGalerie, Bilder = [new AktuellesBild { BildId = verwendet }] }]
        };
        await service.Speichern(beitrag);

        (await VorlaeufigAsync()).ShouldBe(new Dictionary<string, bool> { ["verwendet"] = false, ["spaeter"] = true, ["wieder entfernt"] = true });

        // Weiteres Bild beim Bearbeiten
        beitrag.Bloecke.Single().Bilder.Add(new AktuellesBild { BildId = spaeter, Sortierung = 1 });
        await service.Speichern(beitrag);

        (await VorlaeufigAsync()).ShouldBe(new Dictionary<string, bool> { ["verwendet"] = false, ["spaeter"] = false, ["wieder entfernt"] = true });
    }

    private async Task<List<string>> BildTitelAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Images.OrderBy(i => i.Title).Select(i => i.Title).ToListAsync(Abbruch);
    }

    /// <summary>Beitrag mit zwei Galerien: "a" und "b" in der ersten, "c" in der zweiten.</summary>
    private async Task<int> BeitragMitBildernAsync()
    {
        var (a, b, c) = (await HochladenAsync("a"), await HochladenAsync("b"), await HochladenAsync("c"));
        await using var kontext = Datenbank.NeuerKontext();
        var beitrag = new AktuellesBeitrag
        {
            Titel = "Lehrgang",
            Slug = "lehrgang",
            Bloecke =
            [
                new AktuellesBlock { Typ = AktuellesBlockTyp.BilderGalerie, Sortierung = 0, Bilder = [new AktuellesBild { BildId = a }, new AktuellesBild { BildId = b, Sortierung = 1 }] },
                new AktuellesBlock { Typ = AktuellesBlockTyp.BilderGalerie, Sortierung = 1, Bilder = [new AktuellesBild { BildId = c }] }
            ]
        };
        await new AktuellesService(kontext).Speichern(beitrag);
        return beitrag.Id;
    }

    [DatenbankFact]
    public async Task Entfernte_Bilder_werden_beim_Speichern_geloescht()
    {
        var id = await BeitragMitBildernAsync();

        // Wie die Bearbeiten-Seite: Beitrag laden, Bild "a" entfernen, zweite Galerie entfernen, speichern
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var service = new AktuellesService(kontext);
            var beitrag = (await service.GetById(id))!;
            var erste = beitrag.Bloecke.Single(b => b.Sortierung == 0);
            erste.Bilder.Remove(erste.Bilder.Single(b => b.Bild!.Title == "a"));
            beitrag.Bloecke.Remove(beitrag.Bloecke.Single(b => b.Sortierung == 1));
            await service.Speichern(beitrag);

            // Erneutes Speichern auf demselben Kontext (die Seite bleibt offen) funktioniert weiter
            beitrag.Titel = "Lehrgang 2026";
            await service.Speichern(beitrag);
        }

        (await BildTitelAsync()).ShouldBe(["b"]);
        await using var pruefung = Datenbank.NeuerKontext();
        (await pruefung.AktuellesBilder.CountAsync(Abbruch)).ShouldBe(1);
    }

    [DatenbankFact]
    public async Task Loeschen_entfernt_die_Bilder_des_Beitrags_aber_keine_anderswo_verwendeten()
    {
        var id = await BeitragMitBildernAsync();
        await HochladenAsync("anderer upload");
        await using (var kontext = Datenbank.NeuerKontext())
        {
            // Bild "c" wird zusätzlich von einer Neuigkeit verwendet
            var c = await kontext.Images.SingleAsync(i => i.Title == "c", Abbruch);
            kontext.Neuigkeiten.Add(new Neuigkeit { Titel = "Neuigkeit", DbImageId = c.Id });
            await kontext.SaveChangesAsync(Abbruch);
        }

        await using (var kontext = Datenbank.NeuerKontext())
            await new AktuellesService(kontext).Loeschen(id);

        (await BildTitelAsync()).ShouldBe(["anderer upload", "c"]);
        await using var pruefung = Datenbank.NeuerKontext();
        (await pruefung.AktuellesBeitraege.CountAsync(Abbruch)).ShouldBe(0);
        (await pruefung.Neuigkeiten.SingleAsync(Abbruch)).DbImageId.ShouldNotBeNull();
    }
}
