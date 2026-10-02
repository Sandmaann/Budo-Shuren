using System.Net;
using BudoShurenWebsite.Global;
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
