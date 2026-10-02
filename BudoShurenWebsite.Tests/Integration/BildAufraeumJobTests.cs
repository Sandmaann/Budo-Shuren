using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

[Trait("Category", "Integration")]
public class BildAufraeumJobTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTime Jetzt = new(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LangeVorlaeufig = Jetzt - BildAufraeumJob.AufbewahrenFuer - TimeSpan.FromMinutes(1);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(Jetzt));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private BildAufraeumJob Job => new(new TestKontextFabrik(Datenbank), _zeit);

    private static DbImage Bild(string titel, DateTime? vorlaeufigSeit) =>
        new() { Title = titel, ImageData = [1, 2, 3], ContentType = "image/jpeg", CreatedAt = Jetzt.AddDays(-30), VorlaeufigSeitUtc = vorlaeufigSeit };

    private async Task<List<string>> TitelAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Images.OrderBy(i => i.Title).Select(i => i.Title).ToListAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Loescht_nur_Bilder_die_zu_lange_vorlaeufig_sind()
    {
        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.Images.AddRange(
                Bild("nie gespeichert", LangeVorlaeufig),
                Bild("editor noch offen", Jetzt - BildAufraeumJob.AufbewahrenFuer + TimeSpan.FromMinutes(1)),
                // Bilder ohne Markierung (alle bisherigen, Galerie, Neuigkeiten, Wissen) fasst der Job nie an, auch unverwendet nicht
                Bild("ohne markierung", null));
            await kontext.SaveChangesAsync(Abbruch);
        }

        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(1);

        (await TitelAsync()).ShouldBe(["editor noch offen", "ohne markierung"]);
    }

    [DatenbankFact]
    public async Task Verwendete_Bilder_bleiben_auch_mit_Markierung()
    {
        // Nach dem Speichern ist die Markierung weg. Bliebe sie doch stehen, schützt die Prüfung auf Verwendung jeden Inhalt.
        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.Galerie.Add(new GalerieEintrag { DbImage = Bild("galerie", LangeVorlaeufig) });
            kontext.Neuigkeiten.Add(new Neuigkeit { Titel = "Neuigkeit", DbImage = Bild("neuigkeit", LangeVorlaeufig) });
            kontext.WissenKategorien.Add(new WissenKategorie
            {
                Name = "Allgemein",
                Slug = "allgemein",
                Beitraege =
                {
                    new WissenBeitrag { Titel = "Wissen", Slug = "wissen", Bloecke = { new WissenBlock { Typ = WissenBlockTyp.TextAbsatz, Bild = Bild("wissen", LangeVorlaeufig) } } }
                }
            });
            kontext.AktuellesBeitraege.Add(new AktuellesBeitrag
            {
                Titel = "Aktuelles",
                Slug = "aktuelles",
                Bloecke = { new AktuellesBlock { Typ = AktuellesBlockTyp.BilderGalerie, Bilder = { new AktuellesBild { Bild = Bild("aktuelles", LangeVorlaeufig) } } } }
            });
            kontext.Veranstaltungen.Add(new Veranstaltung
            {
                Titel = "Seminar",
                Slug = "seminar",
                ErstelltUtc = Jetzt,
                Bloecke = { new VeranstaltungBlock { Typ = VeranstaltungBlockTyp.BilderGalerie, Bilder = { new VeranstaltungBild { Bild = Bild("veranstaltung", LangeVorlaeufig) } } } }
            });
            await kontext.SaveChangesAsync(Abbruch);
        }

        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(0);

        (await TitelAsync()).ShouldBe(["aktuelles", "galerie", "neuigkeit", "veranstaltung", "wissen"]);
        await using var pruefung = Datenbank.NeuerKontext();
        // Bei Aktuelles hätte das Löschen des Bildes auch seine Verwendung im Beitrag gelöscht (Cascade)
        (await pruefung.AktuellesBilder.CountAsync(Abbruch)).ShouldBe(1);
    }
}
