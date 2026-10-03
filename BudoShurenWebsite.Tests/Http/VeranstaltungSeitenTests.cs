using System.Net;
using System.Text.RegularExpressions;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
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
            KontaktEmail = "seminar@example.org",
            ErstelltUtc = Start.UtcDateTime,
            Tage =
            {
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = 20 },
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0), MaxTeilnehmer = 20 }
            }
        };
        if (beschreibung.Length > 0)
            v.Bloecke.Add(new VeranstaltungBlock { Typ = VeranstaltungBlockTyp.MarkdownText, MarkdownInhalt = beschreibung });
        await using var kontext = Datenbank.NeuerKontext();
        kontext.Veranstaltungen.Add(v);
        await kontext.SaveChangesAsync(Abbruch);
        return v;
    }

    /// <summary>Hängt eine Galerie mit einem Bild an; liefert die Id des Bildes.</summary>
    private async Task<int> GalerieAnhaengenAsync(Veranstaltung v, string unterschrift)
    {
        await using var kontext = Datenbank.NeuerKontext();
        var bild = new DbImage { Title = "dojo.jpg", ImageData = [1, 2, 3], ContentType = "image/jpeg", CreatedAt = Start.UtcDateTime };
        kontext.VeranstaltungBloecke.Add(new VeranstaltungBlock
        {
            VeranstaltungId = v.Id,
            Typ = VeranstaltungBlockTyp.BilderGalerie,
            Sortierung = 1,
            BilderProReihe = 2,
            BildUnterschrift = unterschrift,
            Bilder = { new VeranstaltungBild { Bild = bild } }
        });
        await kontext.SaveChangesAsync(Abbruch);
        return bild.Id;
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

        html.ShouldContain("href=\"veranstaltungen/oeffentlich\"");
        html.ShouldNotContain("veranstaltungen/geheim");
        html.ShouldNotContain("veranstaltungen/entwurf");
        ModulLinks.SollenRelativSein(html);
    }

    [DatenbankFact]
    public async Task Detailseite_zeigt_Bausteine_und_Angaben_fuer_Suchmaschinen()
    {
        var v = await AnlegenAsync("herbstseminar", beschreibung: "Erster **Text**");
        var bildId = await GalerieAnhaengenAsync(v, "Training <2025>");
        var geheim = await AnlegenAsync("geheim", sichtbarkeit: VeranstaltungSichtbarkeit.NurPerLink);
        var geheimesBild = await GalerieAnhaengenAsync(geheim, "geheim");
        var entwurf = await AnlegenAsync("entwurf", status: VeranstaltungStatus.Entwurf);
        var entwurfsBild = await GalerieAnhaengenAsync(entwurf, "Entwurf");
        await using var app = App();
        using var client = app.CreateClient();

        var html = await client.GetStringAsync("/veranstaltungen/herbstseminar", Abbruch);

        html.IndexOf("Erster <strong>Text</strong>").ShouldBeLessThan(html.IndexOf("galerie-raster"), "Reihenfolge der Bausteine");
        html.ShouldContain($"src=\"Account/Member/Filesave/GetImage/{bildId}\"");
        // "Zur Anmeldung" springt auf dieser Seite zum Formular, auch wenn die Website in einem Unterverzeichnis läuft
        html.ShouldContain("href=\"veranstaltungen/herbstseminar#anmeldung\"");
        ModulLinks.SollenRelativSein(html);
        html.ShouldContain("Training &lt;2025&gt;");
        html.ShouldContain("<link rel=\"canonical\" href=\"https://www.budo-shuren-dojo.de/veranstaltungen/herbstseminar\"");
        html.ShouldContain($"<meta property=\"og:image\" content=\"https://www.budo-shuren-dojo.de/Account/Member/Filesave/GetImage/{bildId}\"");
        // Blazor kodiert das "+" im Attribut als &#x2B;, der Browser liest es wie "+"
        html.ShouldContain("<script type=\"application/ld&#x2B;json\">{\"@context\":\"https://schema.org\",\"@type\":\"Event\"");
        html.ShouldContain("\"startDate\":\"2026-11-14T10:00:00"); // "+01:00" steht JSON-kodiert als +01:00 (VeranstaltungSeoTests)
        html.ShouldNotContain("noindex");

        // Bilder veröffentlichter (auch "nur per Link") Veranstaltungen sind ohne Login abrufbar, Entwürfe nicht
        (await client.GetAsync($"/Account/Member/Filesave/GetImage/{bildId}", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"/Account/Member/Filesave/GetImage/{geheimesBild}", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"/Account/Member/Filesave/GetImage/{entwurfsBild}", Abbruch)).StatusCode.ShouldNotBe(HttpStatusCode.OK);

        // "Nur per Link" soll nicht in Suchmaschinen auftauchen
        var geheimHtml = await client.GetStringAsync("/veranstaltungen/geheim", Abbruch);
        geheimHtml.ShouldContain("<meta name=\"robots\" content=\"noindex\"");
        geheimHtml.ShouldNotContain("\"@type\":\"Event\"");
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
        html.ShouldContain("Dies ist eine private Veranstaltung.");
        html.ShouldContain("Probleme mit der Anmeldung? Schreib an <a href=\"mailto:seminar@example.org\"");

        await using (var kontext = Datenbank.NeuerKontext())
            await kontext.Veranstaltungen.ExecuteUpdateAsync(s => s.SetProperty(v => v.PrivateVeranstaltung, false), Abbruch);
        (await client.GetStringAsync("/veranstaltungen/herbstseminar", Abbruch)).ShouldNotContain("private Veranstaltung");

        (await client.GetAsync("/veranstaltungen/geheim", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/veranstaltungen/entwurf", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/veranstaltungen/gibt-es-nicht", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [DatenbankFact]
    public async Task Eingeloggte_Benutzer_bekommen_Name_und_Email_vorbelegt()
    {
        await AnlegenAsync();
        await using var app = App();
        string userId;
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "mitglied", Email = "mitglied@example.org", Vorname = "Mia", Name = "Mitglied", Verified = true };
            (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
            userId = user.Id;
        }

        using var gast = app.CreateClient();
        var gastAntwort = await gast.GetAsync("/veranstaltungen/herbstseminar", Abbruch);
        (await gastAntwort.Content.ReadAsStringAsync(Abbruch)).ShouldNotContain("mitglied@example.org");

        using var mitglied = app.CreateClient();
        mitglied.DefaultRequestHeaders.Add(TestAnmeldung.Header, userId);
        var antwort = await mitglied.GetAsync("/veranstaltungen/herbstseminar", Abbruch);
        var html = await antwort.Content.ReadAsStringAsync(Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("value=\"Mia\"");
        html.ShouldContain("value=\"Mitglied\"");
        html.ShouldContain("value=\"mitglied@example.org\"");
        // Persönliche Daten in der Seite: sie darf nicht gecacht werden (setzt Antiforgery)
        antwort.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
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
        // Nach dem Absenden steht die Seite oben: die Rückmeldung auch
        html.IndexOf("Fast geschafft").ShouldBeLessThan(html.IndexOf("id=\"anmeldung\""));
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.Anmeldungen.SingleAsync(Abbruch)).Status.ShouldBe(AnmeldungStatus.Unbestaetigt);
        (await kontext.EmailAusgang.SingleAsync(Abbruch)).Html.ShouldContain("http://localhost/veranstaltungen/bestaetigen/");
    }

    [DatenbankFact]
    public async Task Erneute_Anmeldung_derselben_Adresse_bekommt_dieselbe_zutreffende_Meldung()
    {
        await AnlegenAsync();
        await using var app = App();
        using var client = app.CreateClient();
        await AnmeldenUeberFormularAsync(client, TimeSpan.FromSeconds(10));
        await using (var kontext = Datenbank.NeuerKontext())
            await kontext.Anmeldungen.ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AnmeldungStatus.Angemeldet), Abbruch);

        var (status, html) = await AnmeldenUeberFormularAsync(client, TimeSpan.FromSeconds(10));

        status.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Fast geschafft");
        html.ShouldContain("Warst du schon angemeldet, findest du in der E-Mail stattdessen den Link zu deiner bestehenden Anmeldung.");
        await using var pruefen = Datenbank.NeuerKontext();
        (await pruefen.Anmeldungen.SingleAsync(Abbruch)).Status.ShouldBe(AnmeldungStatus.Angemeldet);
        (await pruefen.EmailAusgang.OrderBy(m => m.Id).LastAsync(Abbruch)).Html.ShouldContain("http://localhost/veranstaltungen/meine-anmeldung/");
    }

    [DatenbankFact]
    public async Task Fehlende_Angaben_werden_am_Feld_gemeldet()
    {
        await AnlegenAsync();
        await using var app = App();
        using var client = app.CreateClient();

        var (_, html) = await AnmeldenUeberFormularAsync(client, TimeSpan.FromSeconds(10), f => f["Formular.Eingabe.Vorname"] = "");

        html.ShouldContain("Bitte gib deinen Vornamen an.");
        html.IndexOf("Bitte prüfe deine Angaben").ShouldBeLessThan(html.IndexOf("id=\"anmeldung\""));
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
        // So schickt der Browser die Formulare ab (EditForm mit Enhance)
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html; blazor-enhanced-nav=on");

        var antworten = new List<HttpResponseMessage>();
        for (var i = 0; i <= VeranstaltungRateLimit.ErlaubteAnfragen; i++)
            antworten.Add(await client.PostAsync(url, new FormUrlEncodedContent([]), Abbruch));

        antworten.Take(VeranstaltungRateLimit.ErlaubteAnfragen).Select(a => a.StatusCode).ShouldNotContain(HttpStatusCode.TooManyRequests);
        var abgelehnt = antworten.Last();
        abgelehnt.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        abgelehnt.Headers.RetryAfter!.Delta!.Value.ShouldBeInRange(TimeSpan.FromSeconds(1), VeranstaltungRateLimit.Zeitfenster);
        abgelehnt.Headers.CacheControl!.NoStore.ShouldBeTrue("der Link zurück enthält das Token");
        abgelehnt.Headers.GetValues("blazor-enhanced-nav").ShouldBe(["allow"], "sonst zeigt blazor.web.js statt der Seite nur eine Fehlerzeile");
        abgelehnt.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");

        // Statt einer leeren Antwort eine Seite, die erklärt, was los ist
        var html = await abgelehnt.Content.ReadAsStringAsync(Abbruch);
        html.ShouldContain("Zu viele Anfragen");
        html.ShouldContain("Deine letzte Eingabe wurde nicht übernommen");
        html.ShouldContain("Minute");
        html.ShouldContain($"href=\"{url.TrimStart('/')}\"");
        html.ShouldContain("<footer", customMessage: "mit dem Layout der Website");
        ModulLinks.SollenRelativSein(html);

        (await client.GetAsync(url, Abbruch)).StatusCode.ShouldBe(HttpStatusCode.OK, "Seiten aufrufen ist nicht begrenzt");
    }

    [DatenbankFact]
    public async Task Hinweisseite_zu_vielen_Anfragen_gibt_es_nur_nach_einer_Ablehnung()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var antwort = await client.GetAsync(VeranstaltungRateLimit.HinweisSeite, Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await antwort.Content.ReadAsStringAsync(Abbruch)).ShouldNotContain("Zu viele Anfragen</h2>");
    }
}
