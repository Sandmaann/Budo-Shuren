using System.Net;
using System.Text.RegularExpressions;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>Öffentliche Seiten über die komplette App: Formular-POSTs mit Antiforgery, Header, Rate-Limit.</summary>
[Trait("Category", "Integration")]
public class VeranstaltungSeitenTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _zeit = new(Start);

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private TestWebAppFactory App(bool aktiviert = true) => new(Datenbank.Verbindung, aktiviert, _zeit);

    private async Task<Veranstaltung> AnlegenAsync(
        string slug = "herbstseminar",
        VeranstaltungStatus status = VeranstaltungStatus.Veroeffentlicht,
        VeranstaltungSichtbarkeit sichtbarkeit = VeranstaltungSichtbarkeit.Oeffentlich,
        string beschreibung = "")
    {
        var v = new Veranstaltung
        {
            Titel = $"Titel {slug}",
            Slug = slug,
            Status = status,
            Sichtbarkeit = sichtbarkeit,
            Beschreibung = beschreibung,
            KontaktEmail = "seminar@example.org",
            ErstelltUtc = Start.UtcDateTime,
            Tage =
            {
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = 20 },
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0), MaxTeilnehmer = 20 }
            }
        };
        await using var kontext = Datenbank.NeuerKontext();
        kontext.Veranstaltungen.Add(v);
        await kontext.SaveChangesAsync(Abbruch);
        return v;
    }

    private static Dictionary<string, string> MitAnmeldedaten(Dictionary<string, string> felder, string vorname = "Max")
    {
        felder["Formular.Eingabe.Vorname"] = vorname;
        felder["Formular.Eingabe.Nachname"] = "Muster";
        felder["Formular.Eingabe.Email"] = "max@example.org";
        felder["Formular.Eingabe.AnzahlBegleitpersonen"] = "0";
        felder["Formular.Eingabe.DatenschutzAkzeptiert"] = "true";
        felder["Formular.Website"] = "";
        return felder;
    }

    private async Task<(HttpStatusCode Status, string Html)> AnmeldenUeberFormularAsync(HttpClient client, TimeSpan wartezeit, Action<Dictionary<string, string>>? aendern = null)
    {
        var seite = await client.GetStringAsync("/veranstaltungen/herbstseminar", Abbruch);
        var felder = MitAnmeldedaten(HtmlFormular.VersteckteFelder(seite, "anmeldung"));
        aendern?.Invoke(felder);
        _zeit.Advance(wartezeit);

        var antwort = await client.PostAsync("/veranstaltungen/herbstseminar", new FormUrlEncodedContent(felder), Abbruch);
        return (antwort.StatusCode, await antwort.Content.ReadAsStringAsync(Abbruch));
    }

    private async Task<int> AnmeldungenAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Anmeldungen.CountAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Ausgeschaltetes_Modul_liefert_404()
    {
        await AnlegenAsync();
        await using var app = App(aktiviert: false);
        using var client = app.CreateClient();

        (await client.GetAsync("/veranstaltungen", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/veranstaltungen/herbstseminar", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/veranstaltungen/bestaetigen/{AnmeldeToken.Erzeugen().Klartext}", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [DatenbankFact]
    public async Task Liste_zeigt_nur_oeffentliche_veroeffentlichte_Veranstaltungen()
    {
        await AnlegenAsync("oeffentlich");
        await AnlegenAsync("geheim", sichtbarkeit: VeranstaltungSichtbarkeit.NurPerLink);
        await AnlegenAsync("entwurf", status: VeranstaltungStatus.Entwurf);
        await using var app = App();
        using var client = app.CreateClient();

        var html = await client.GetStringAsync("/veranstaltungen", Abbruch);

        html.ShouldContain("/veranstaltungen/oeffentlich");
        html.ShouldNotContain("/veranstaltungen/geheim");
        html.ShouldNotContain("/veranstaltungen/entwurf");
    }

    [DatenbankFact]
    public async Task Detailseite_fuer_Veroeffentlichte_und_per_Link_404_fuer_Entwuerfe()
    {
        await AnlegenAsync("herbstseminar", beschreibung: "**Programm** <script>alert('x')</script>");
        await AnlegenAsync("geheim", sichtbarkeit: VeranstaltungSichtbarkeit.NurPerLink);
        await AnlegenAsync("entwurf", status: VeranstaltungStatus.Entwurf);
        await using var app = App();
        using var client = app.CreateClient();

        var antwort = await client.GetAsync("/veranstaltungen/herbstseminar", Abbruch);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await antwort.Content.ReadAsStringAsync(Abbruch);
        html.ShouldContain("Titel herbstseminar");
        html.ShouldContain("<strong>Programm</strong>");
        html.ShouldNotContain("<script>alert");
        html.ShouldContain("name=\"_handler\" value=\"anmeldung\"");

        (await client.GetAsync("/veranstaltungen/geheim", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/veranstaltungen/entwurf", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/veranstaltungen/gibt-es-nicht", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [DatenbankFact]
    public async Task Anmeldung_ueber_das_Formular()
    {
        await AnlegenAsync();
        await using var app = App();
        using var client = app.CreateClient();

        var (status, html) = await AnmeldenUeberFormularAsync(client, TimeSpan.FromSeconds(10));

        status.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Fast geschafft");
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.Anmeldungen.SingleAsync(Abbruch)).Status.ShouldBe(AnmeldungStatus.Unbestaetigt);
        (await kontext.EmailAusgang.SingleAsync(Abbruch)).Html.ShouldContain("http://localhost/veranstaltungen/bestaetigen/");
    }

    [DatenbankFact]
    public async Task Fehlende_Angaben_werden_am_Feld_gemeldet()
    {
        await AnlegenAsync();
        await using var app = App();
        using var client = app.CreateClient();

        var (_, html) = await AnmeldenUeberFormularAsync(client, TimeSpan.FromSeconds(10), f => f["Formular.Eingabe.Vorname"] = "");

        html.ShouldContain("Bitte gib deinen Vornamen an.");
        html.ShouldNotContain("Fast geschafft");
        (await AnmeldungenAsync()).ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Ausgefuellter_Honeypot_scheint_erfolgreich_speichert_aber_nichts()
    {
        await AnlegenAsync();
        await using var app = App();
        using var client = app.CreateClient();

        var (_, html) = await AnmeldenUeberFormularAsync(client, TimeSpan.FromSeconds(10), f => f["Formular.Website"] = "https://spam.example");

        html.ShouldContain("Fast geschafft");
        (await AnmeldungenAsync()).ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Zu_schnelles_Absenden_scheint_erfolgreich_speichert_aber_nichts()
    {
        await AnlegenAsync();
        await using var app = App();
        using var client = app.CreateClient();

        var (_, html) = await AnmeldenUeberFormularAsync(client, TimeSpan.FromMilliseconds(500));

        html.ShouldContain("Fast geschafft");
        (await AnmeldungenAsync()).ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Bestaetigungslink_oeffnen_aendert_nichts_erst_der_Klick_bestaetigt()
    {
        var v = await AnlegenAsync();
        var token = AnmeldeToken.Erzeugen();
        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.Anmeldungen.Add(new Anmeldung
            {
                VeranstaltungId = v.Id,
                Email = "max@example.org",
                Vorname = "Max",
                Nachname = "Muster",
                TokenHash = token.Hash,
                ReserviertBisUtc = Start.UtcDateTime.AddHours(24),
                ErstelltUtc = Start.UtcDateTime
            });
            await kontext.SaveChangesAsync(Abbruch);
        }
        await using var app = App();
        using var client = app.CreateClient();
        var url = $"/veranstaltungen/bestaetigen/{token.Klartext}";

        var seite = await client.GetAsync(url, Abbruch);
        seite.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        seite.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var html = await seite.Content.ReadAsStringAsync(Abbruch);
        html.ShouldContain("Titel herbstseminar");
        await using (var kontext = Datenbank.NeuerKontext())
            (await kontext.Anmeldungen.SingleAsync(Abbruch)).Status.ShouldBe(AnmeldungStatus.Unbestaetigt, "ein GET (z. B. Mail-Scanner) darf nichts ändern");

        var antwort = await client.PostAsync(url, new FormUrlEncodedContent(HtmlFormular.VersteckteFelder(html, "bestaetigen")), Abbruch);

        (await antwort.Content.ReadAsStringAsync(Abbruch)).ShouldContain("Anmeldung bestätigt");
        await using (var kontext = Datenbank.NeuerKontext())
            (await kontext.Anmeldungen.SingleAsync(Abbruch)).Status.ShouldBe(AnmeldungStatus.Angemeldet);
    }

    [DatenbankFact]
    public async Task Ungueltiger_Bestaetigungslink_zeigt_einen_Hinweis()
    {
        await AnlegenAsync();
        await using var app = App();
        using var client = app.CreateClient();

        (await client.GetStringAsync("/veranstaltungen/bestaetigen/kaputt", Abbruch)).ShouldContain("Link ungültig");
    }

    [DatenbankFact]
    public async Task Zu_viele_Formular_Anfragen_werden_begrenzt()
    {
        await using var app = App();
        using var client = app.CreateClient();
        var url = $"/veranstaltungen/bestaetigen/{AnmeldeToken.Erzeugen().Klartext}";

        var statuscodes = new List<HttpStatusCode>();
        for (var i = 0; i <= VeranstaltungRateLimit.ErlaubteAnfragen; i++)
            statuscodes.Add((await client.PostAsync(url, new FormUrlEncodedContent([]), Abbruch)).StatusCode);

        statuscodes.Take(VeranstaltungRateLimit.ErlaubteAnfragen).ShouldNotContain(HttpStatusCode.TooManyRequests);
        statuscodes.Last().ShouldBe(HttpStatusCode.TooManyRequests);
        (await client.GetAsync(url, Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK, "Seiten aufrufen ist nicht begrenzt");
    }
}
