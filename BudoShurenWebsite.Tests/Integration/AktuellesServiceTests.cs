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
}
