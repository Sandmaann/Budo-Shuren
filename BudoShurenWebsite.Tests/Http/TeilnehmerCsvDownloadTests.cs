using System.Net;
using System.Text;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>CSV-Download der Übersichtsseite: Login, Freischaltung, Rollen, Abteilung und Feature-Schalter.</summary>
[Trait("Category", "Integration")]
public class TeilnehmerCsvDownloadTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private async Task<int> VeranstaltungAnlegenAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        kontext.Abteilungen.Add(new Abteilung { ID = "Aikido", Name = "Aikido" });
        var v = new Veranstaltung
        {
            Titel = "Herbstseminar",
            Slug = "herbstseminar",
            AbteilungId = "Aikido",
            Status = VeranstaltungStatus.Veroeffentlicht,
            ErstelltUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc),
            Tage = { new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0) } },
            Anmeldungen =
            {
                new Anmeldung
                {
                    Email = "max@example.org", Vorname = "Max", Nachname = "Müller", Status = AnmeldungStatus.Angemeldet,
                    ErstelltUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        kontext.Veranstaltungen.Add(v);
        await kontext.SaveChangesAsync(Abbruch);
        return v.Id;
    }

    /// <summary>Legt den Benutzer über Identity an (die Rollen hat der App-Start angelegt).</summary>
    private static async Task<string> BenutzerAsync(TestWebAppFactory app, string name, string rolle, bool freigeschaltet = true, string abteilung = "")
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@example.org", Vorname = name, Verified = freigeschaltet, Abteilung = abteilung };
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        (await userManager.AddToRoleAsync(user, rolle)).Succeeded.ShouldBeTrue();
        return user.Id;
    }

    private static HttpClient Client(TestWebAppFactory app, string? userId)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (userId is not null)
            client.DefaultRequestHeaders.Add(TestAnmeldung.Header, userId);
        return client;
    }

    private static string Pfad(int id) => $"/Account/Member/Veranstaltungen/{id}/teilnehmer.csv";

    [DatenbankFact]
    public async Task Admin_bekommt_die_CSV_als_Download()
    {
        var id = await VeranstaltungAnlegenAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        var admin = await BenutzerAsync(app, "admin", Roles.Admin);

        var antwort = await Client(app, admin).GetAsync(Pfad(id), Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        antwort.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        antwort.Content.Headers.ContentDisposition.FileNameStar.ShouldEndWith(".csv");
        var inhalt = await antwort.Content.ReadAsByteArrayAsync(Abbruch);
        Encoding.UTF8.GetString(inhalt).ShouldContain("\"Müller\";\"Max\"");
    }

    [DatenbankFact]
    public async Task Abteilungsleiter_der_eigenen_Abteilung_darf()
    {
        var id = await VeranstaltungAnlegenAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        var leiter = await BenutzerAsync(app, "leiter", Roles.Abteilungsleiter, abteilung: "Aikido");

        (await Client(app, leiter).GetAsync(Pfad(id), Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [DatenbankFact]
    public async Task Fremde_Abteilung_und_unbekannte_Veranstaltung_liefern_404()
    {
        var id = await VeranstaltungAnlegenAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        var leiter = await BenutzerAsync(app, "leiter", Roles.Abteilungsleiter, abteilung: "Karate");
        var admin = await BenutzerAsync(app, "admin", Roles.Admin);

        (await Client(app, leiter).GetAsync(Pfad(id), Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client(app, admin).GetAsync(Pfad(id + 1000), Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [DatenbankFact]
    public async Task Anonym_wird_zum_Login_geleitet()
    {
        var id = await VeranstaltungAnlegenAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);

        var antwort = await Client(app, null).GetAsync(Pfad(id), Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        antwort.Headers.Location!.ToString().ShouldContain("/Account/Login");
    }

    [DatenbankFact]
    public async Task Ohne_Freischaltung_oder_mit_falscher_Rolle_kein_Zugriff()
    {
        var id = await VeranstaltungAnlegenAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        var nichtFreigeschaltet = await BenutzerAsync(app, "neu", Roles.Admin, freigeschaltet: false);
        var editor = await BenutzerAsync(app, "editor", Roles.Editor);

        // Der Test-Login antwortet auf "verboten" mit 403 (das Cookie leitet stattdessen auf AccessDenied weiter)
        foreach (var userId in new[] { nichtFreigeschaltet, editor })
            (await Client(app, userId).GetAsync(Pfad(id), Abbruch)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [DatenbankFact]
    public async Task Ausgeschaltetes_Modul_liefert_404()
    {
        var id = await VeranstaltungAnlegenAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung, veranstaltungenAktiviert: false);
        var admin = await BenutzerAsync(app, "admin", Roles.Admin);

        (await Client(app, admin).GetAsync(Pfad(id), Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
