using System.Linq.Expressions;
using System.Net;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>Besucherzähler im MainLayout: Cookie in der Anfrage, Speichern im Hintergrund.</summary>
[Trait("Category", "Integration")]
public class BesucherZaehlerTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Browser = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0 Safari/537.36";

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private static HttpRequestMessage Anfrage(string pfad, string userAgent, string? besucherId = null)
    {
        var anfrage = new HttpRequestMessage(HttpMethod.Get, pfad);
        anfrage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (besucherId != null)
            anfrage.Headers.Add("Cookie", $"VisitorId={besucherId}");
        return anfrage;
    }

    private static string? GesetzteBesucherId(HttpResponseMessage antwort) =>
        antwort.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Where(c => c.StartsWith("VisitorId=")).Select(c => c["VisitorId=".Length..].Split(';')[0]).SingleOrDefault()
            : null;

    /// <summary>
    /// Der Besuch wird im Hintergrund gespeichert: kurz darauf warten.
    /// Gezählt werden nur die Besuche dieses Tests (<paramref name="eigene"/>): Hintergrund-Tasks früherer Tests
    /// speichern ihren Besuch manchmal erst, nachdem die Datenbank für diesen Test zurückgesetzt wurde.
    /// </summary>
    private async Task<List<Visit>> BesucheAsync(Expression<Func<Visit, bool>> eigene)
    {
        for (var versuch = 0; ; versuch++)
        {
            await using var kontext = Datenbank.NeuerKontext();
            var besuche = await kontext.Visits.AsNoTracking().Where(eigene).OrderBy(v => v.ID).ToListAsync(Abbruch);
            if (besuche.Count >= 1 || versuch >= 50)
                return besuche;
            await Task.Delay(100, Abbruch);
        }
    }

    [DatenbankFact]
    public async Task Neuer_Besucher_bekommt_das_Cookie_und_wird_unter_dieser_Id_gezaehlt()
    {
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient(new() { HandleCookies = false });

        var antwort = await client.SendAsync(Anfrage("/Impressum", Browser), Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var besucherId = GesetzteBesucherId(antwort).ShouldNotBeNull();
        Guid.TryParse(besucherId, out _).ShouldBeTrue();
        var besuch = (await BesucheAsync(b => b.VisitorID == besucherId)).ShouldHaveSingleItem();
        besuch.PageName.ShouldBe("Impressum");
    }

    [DatenbankFact]
    public async Task Bekannter_Besucher_behaelt_seine_Id_und_bekommt_kein_neues_Cookie()
    {
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient(new() { HandleCookies = false });

        var antwort = await client.SendAsync(Anfrage("/", Browser, besucherId: "bekannt-123"), Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        GesetzteBesucherId(antwort).ShouldBeNull();
        var besuch = (await BesucheAsync(b => b.VisitorID == "bekannt-123")).ShouldHaveSingleItem();
        besuch.PageName.ShouldBe("Home");
    }

    [DatenbankFact]
    public async Task Bots_bekommen_kein_Cookie_und_werden_nicht_gezaehlt()
    {
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient(new() { HandleCookies = false });

        // Eine Seite, die nur dieser Test aufruft: so gehören alle Besuche dieser Seite sicher zu ihm
        var seite = $"veranstaltungen/meine-anmeldung/bot-test-{Guid.NewGuid():N}";

        var bot = await client.SendAsync(Anfrage("/" + seite, "Mozilla/5.0 (compatible; Googlebot/2.1)"), Abbruch);
        // Der Besuch eines Menschen danach zeigt, dass der Hintergrund-Task des Bots (falls es einen gäbe) längst fertig wäre
        await client.SendAsync(Anfrage("/" + seite, Browser, besucherId: "mensch"), Abbruch);

        GesetzteBesucherId(bot).ShouldBeNull();
        (await BesucheAsync(b => b.PageName == seite)).Select(b => b.VisitorID).ShouldBe(["mensch"]);
    }

    [DatenbankFact]
    public async Task Seite_mit_eigenen_Headern_und_neuem_Besucher_liefert_beides()
    {
        // Früher setzte ein Hintergrund-Task das Cookie, während die Seite ihre Header setzte (seltener Fehler 500)
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient(new() { HandleCookies = false });

        for (var i = 0; i < 20; i++)
        {
            var antwort = await client.SendAsync(Anfrage("/veranstaltungen/meine-anmeldung/gibt-es-nicht", Browser), Abbruch);

            ((int)antwort.StatusCode).ShouldBeLessThan(500);
            GesetzteBesucherId(antwort).ShouldNotBeNull();
            antwort.Headers.GetValues("X-Robots-Tag").ShouldBe(["noindex, nofollow"]);
        }
    }
}
