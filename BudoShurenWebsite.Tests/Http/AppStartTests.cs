using System.Net;
using System.Text.RegularExpressions;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Tests.Infrastruktur;

namespace BudoShurenWebsite.Tests.Http;

[Trait("Category", "Integration")]
public class AppStartTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    [DatenbankFact]
    public async Task App_startet_und_Startseite_liefert_200()
    {
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        var antwort = await client.GetAsync("/", TestContext.Current.CancellationToken);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [DatenbankFact]
    public async Task Startseite_liefert_alle_Neuigkeiten_im_vorgerenderten_HTML()
    {
        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.Neuigkeiten.AddRange(
                new Neuigkeit { Titel = "Erste Neuigkeit", Beschreibung = "Text eins", Sortierung = 1 },
                new Neuigkeit { Titel = "Zweite Neuigkeit", Beschreibung = "Text zwei", Sortierung = 2 },
                new Neuigkeit { Titel = "Dritte Neuigkeit", Beschreibung = "Text drei", Sortierung = 3 });
            await kontext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        // Für Suchmaschinen stehen alle Neuigkeiten mit Text im HTML, auch wenn (per CSS) nur die erste sichtbar ist
        var stapel = Regex.Match(html, "<div class=\"neuigkeiten-stapel *\">(.*?)</article>\\s*</div>", RegexOptions.Singleline);
        stapel.Success.ShouldBeTrue();
        Regex.Matches(stapel.Value, "<article[^>]*>(.*?)</article>", RegexOptions.Singleline)
            .Select(a => Regex.Match(a.Groups[1].Value, @"\w+ Neuigkeit").Value)
            .ShouldBe(["Erste Neuigkeit", "Zweite Neuigkeit", "Dritte Neuigkeit"]);
        stapel.Value.ShouldContain("Text drei");
    }

    [DatenbankFact]
    public async Task App_loggt_in_der_Testumgebung_nicht_an_BetterStack()
    {
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient(); // startet die App und damit Program.Main

        var ziele = NLog.LogManager.Configuration?.AllTargets ?? [];

        ziele.ShouldNotBeEmpty("die NLog-Konfiguration für die Umgebung Test wurde nicht geladen");
        ziele.ShouldNotContain(ziel => ziel.GetType().FullName!.Contains("BetterStack"));
    }

    [DatenbankFact]
    public async Task Impressum_zeigt_die_Version_der_Website()
    {
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        var html = await client.GetStringAsync("/Impressum", TestContext.Current.CancellationToken);

        html.ShouldContain($"Website-Version {AppVersion.Text}");
    }
}
