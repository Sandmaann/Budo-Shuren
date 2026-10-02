using System.Net;
using System.Text.Json;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Systemzustand;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>GET /health und /health/alle: nie anonym, nur Admins oder mit Token.</summary>
[Trait("Category", "Integration")]
public class SystemzustandTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private static async Task<string> BenutzerAsync(TestWebAppFactory app, string rolle)
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = rolle, Email = $"{rolle}@example.org", Vorname = rolle, Verified = true };
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        (await userManager.AddToRoleAsync(user, rolle)).Succeeded.ShouldBeTrue();
        return user.Id;
    }

    private static HttpClient MitToken(TestWebAppFactory app, string token = TestWebAppFactory.HealthToken)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(SystemzustandPruefung.TokenHeader, token);
        return client;
    }

    private static List<string> Pruefungen(JsonDocument json) =>
        json.RootElement.GetProperty("pruefungen").EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToList();

    [DatenbankFact]
    public async Task Ohne_Admin_oder_Token_kein_Zugriff()
    {
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        var mitglied = await BenutzerAsync(app, Roles.Mitglied);

        using var anonym = app.CreateClient();
        using var falscherToken = MitToken(app, "falsch");
        using var eingeloggt = app.CreateClient();
        eingeloggt.DefaultRequestHeaders.Add(TestAnmeldung.Header, mitglied);

        foreach (var client in new[] { anonym, falscherToken, eingeloggt })
        foreach (var pfad in new[] { "/health", "/health/alle" })
        {
            var antwort = await client.GetAsync(pfad, Abbruch);
            antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, pfad);
            (await antwort.Content.ReadAsStringAsync(Abbruch)).ShouldBeEmpty();
        }
    }

    [DatenbankFact]
    public async Task Mit_Token_liefert_health_die_Betriebspruefungen()
    {
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = MitToken(app);

        var antwort = await client.GetAsync("/health", Abbruch);

        // In den Tests sind Mailversand und Jobs abgeschaltet: eingeschränkt, aber erreichbar
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        using var json = JsonDocument.Parse(await antwort.Content.ReadAsStringAsync(Abbruch));
        json.RootElement.GetProperty("status").GetString().ShouldBe("Degraded");
        json.RootElement.GetProperty("version").GetString().ShouldBe(AppVersion.Text);
        Pruefungen(json).ShouldBe(["Datenbank", "Hintergrunddienste", "Mail-Warteschlange"]);
        // Alle drei Dienste haben sich beim Start gemeldet, hier als (ungewollt) abgeschaltet
        var dienste = json.RootElement.GetProperty("pruefungen").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "Hintergrunddienste")
            .GetProperty("beschreibung").GetString().ShouldNotBeNull();
        dienste.ShouldContain($"{DienstHerzschlag.EmailVersand}: abgeschaltet (EmailVersand:Aktiviert=false)");
        dienste.ShouldContain($"{DienstHerzschlag.Veranstaltungen}: abgeschaltet (Veranstaltungen:HintergrundJobsAktiviert=false)");
        dienste.ShouldContain($"{DienstHerzschlag.BildAufraeumen}: abgeschaltet (BildAufraeumen:Aktiviert=false)");
    }

    [DatenbankFact]
    public async Task Admin_bekommt_unter_health_alle_auch_die_Datenpruefungen()
    {
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAnmeldung.Header, await BenutzerAsync(app, Roles.Admin));

        var antwort = await client.GetAsync("/health/alle", Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await antwort.Content.ReadAsStringAsync(Abbruch));
        Pruefungen(json).ShouldBe(
            ["Datenbank", "Hintergrunddienste", "Mail-Warteschlange", "Bilder", "Veranstaltungen: Wartung", "Veranstaltungen: Kalender", "Veranstaltungen: Anmeldungen"],
            ignoreOrder: true);
    }

    [DatenbankFact]
    public async Task Haengender_Mailversand_ist_ungesund_mit_503()
    {
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var alt = DateTime.UtcNow.AddHours(-2);
            kontext.EmailAusgang.Add(new EmailAusgang { An = "x@example.org", Betreff = "B", Html = "H", Status = EmailStatus.Wartend, ErstelltUtc = alt, FaelligAbUtc = alt });
            await kontext.SaveChangesAsync(Abbruch);
        }
        await using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = MitToken(app);

        var antwort = await client.GetAsync("/health", Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var text = await antwort.Content.ReadAsStringAsync(Abbruch);
        text.ShouldContain("\"status\": \"Unhealthy\"");
        text.ShouldNotContain("x@example.org");
    }
}
