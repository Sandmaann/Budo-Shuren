using System.Net;
using System.Net.Http.Headers;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>Bildabruf über den FilesaveController: Cache-Header, ETag und Berechtigung.</summary>
[Trait("Category", "Integration")]
public class BildAbrufTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly byte[] Daten = [1, 2, 3, 4];

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private static DbImage Bild() =>
        new() { Title = "bild.jpg", ImageData = Daten, ContentType = "image/jpeg", CreatedAt = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc) };

    /// <summary>Bild einer Neuigkeit: ohne Anmeldung abrufbar.</summary>
    private async Task<int> OeffentlichesBildAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        var neuigkeit = new Neuigkeit { Titel = "Neuigkeit", DbImage = Bild() };
        kontext.Neuigkeiten.Add(neuigkeit);
        await kontext.SaveChangesAsync(Abbruch);
        return neuigkeit.DbImage!.Id;
    }

    /// <summary>Bild ohne öffentliche Verwendung: nur für angemeldete Mitglieder.</summary>
    private async Task<int> InternesBildAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        var bild = Bild();
        kontext.Images.Add(bild);
        await kontext.SaveChangesAsync(Abbruch);
        return bild.Id;
    }

    private static async Task<HttpClient> AdminClientAsync(TestWebAppFactory app)
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "admin", Email = "admin@example.org", Vorname = "Admin", Verified = true };
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        (await userManager.AddToRoleAsync(user, Roles.Admin)).Succeeded.ShouldBeTrue();

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAnmeldung.Header, user.Id);
        return client;
    }

    private static string Url(int bildId) => $"/Account/Member/Filesave/GetImage/{bildId}";

    [DatenbankFact]
    public async Task Oeffentliches_Bild_darf_der_Browser_eine_Woche_behalten()
    {
        var bildId = await OeffentlichesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        var antwort = await client.GetAsync(Url(bildId), Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType?.MediaType.ShouldBe("image/jpeg");
        (await antwort.Content.ReadAsByteArrayAsync(Abbruch)).ShouldBe(Daten);
        antwort.Headers.CacheControl.ShouldNotBeNull();
        antwort.Headers.CacheControl.Public.ShouldBeTrue();
        antwort.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(7));
        antwort.Headers.ETag.ShouldNotBeNull();
    }

    [DatenbankFact]
    public async Task Bekanntes_Bild_wird_nicht_erneut_uebertragen()
    {
        var bildId = await OeffentlichesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();
        var etag = (await client.GetAsync(Url(bildId), Abbruch)).Headers.ETag.ShouldNotBeNull();

        using var anfrage = new HttpRequestMessage(HttpMethod.Get, Url(bildId));
        anfrage.Headers.IfNoneMatch.Add(etag);
        var antwort = await client.SendAsync(anfrage, Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await antwort.Content.ReadAsByteArrayAsync(Abbruch)).ShouldBeEmpty();
        antwort.Headers.ETag.ShouldBe(etag);
    }

    [DatenbankFact]
    public async Task Veralteter_ETag_liefert_das_Bild()
    {
        var bildId = await OeffentlichesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        using var anfrage = new HttpRequestMessage(HttpMethod.Get, Url(bildId));
        anfrage.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"anderes-bild\""));
        var antwort = await client.SendAsync(anfrage, Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.ReadAsByteArrayAsync(Abbruch)).ShouldBe(Daten);
    }

    [DatenbankFact]
    public async Task Internes_Bild_bleibt_ohne_Anmeldung_gesperrt_auch_mit_passendem_ETag()
    {
        var bildId = await InternesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var admin = await AdminClientAsync(app);
        using var anonym = app.CreateClient();
        var etag = (await admin.GetAsync(Url(bildId), Abbruch)).Headers.ETag.ShouldNotBeNull();

        using var anfrage = new HttpRequestMessage(HttpMethod.Get, Url(bildId));
        anfrage.Headers.IfNoneMatch.Add(etag);
        var antwort = await anonym.SendAsync(anfrage, Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [DatenbankFact]
    public async Task Internes_Bild_wird_bei_jedem_Abruf_neu_geprueft()
    {
        var bildId = await InternesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var admin = await AdminClientAsync(app);

        var antwort = await admin.GetAsync(Url(bildId), Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Headers.CacheControl.ShouldNotBeNull();
        antwort.Headers.CacheControl.Private.ShouldBeTrue();
        antwort.Headers.CacheControl.NoCache.ShouldBeTrue();
        antwort.Headers.CacheControl.Public.ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Variante_liefert_die_verkleinerte_Fassung_mit_eigenem_ETag()
    {
        int bildId;
        await using (var kontext = Datenbank.NeuerKontext())
        {
            using var foto = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1920, 1080);
            using var jpeg = new MemoryStream();
            SixLabors.ImageSharp.ImageExtensions.SaveAsJpeg(foto, jpeg);
            var bild = Bild();
            bild.ImageData = jpeg.ToArray();
            kontext.Neuigkeiten.Add(new Neuigkeit { Titel = "Neuigkeit", DbImage = bild });
            await kontext.SaveChangesAsync(Abbruch);
            bildId = bild.Id;
        }
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        var original = await client.GetAsync(Url(bildId), Abbruch);
        var variante = await client.GetAsync(Url(bildId) + "?variante=neuigkeit", Abbruch);

        variante.StatusCode.ShouldBe(HttpStatusCode.OK);
        variante.Content.Headers.ContentType?.MediaType.ShouldBe("image/webp");
        var info = SixLabors.ImageSharp.Image.Identify(await variante.Content.ReadAsByteArrayAsync(Abbruch));
        (info.Width, info.Height).ShouldBe((896, 672));
        variante.Headers.CacheControl?.MaxAge.ShouldBe(TimeSpan.FromDays(7));
        variante.Headers.ETag.ShouldNotBeNull().ShouldNotBe(original.Headers.ETag);
        original.Content.Headers.ContentType?.MediaType.ShouldBe("image/jpeg");
    }

    [DatenbankFact]
    public async Task Variante_eines_unlesbaren_Bildes_liefert_das_Original()
    {
        var bildId = await OeffentlichesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        var antwort = await client.GetAsync(Url(bildId) + "?variante=neuigkeit", Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType?.MediaType.ShouldBe("image/jpeg");
        (await antwort.Content.ReadAsByteArrayAsync(Abbruch)).ShouldBe(Daten);
    }

    [DatenbankFact]
    public async Task Variante_bleibt_ohne_Anmeldung_gesperrt_wenn_das_Bild_intern_ist()
    {
        var bildId = await InternesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        (await client.GetAsync(Url(bildId) + "?variante=neuigkeit", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [DatenbankFact]
    public async Task Unbekannte_Variante_wird_abgelehnt()
    {
        var bildId = await OeffentlichesBildAsync();
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        (await client.GetAsync(Url(bildId) + "?variante=gibtesnicht", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync(Url(bildId) + "?variante=99", Abbruch)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [DatenbankFact]
    public async Task Galerie_Bilder_sind_von_der_Bildersuche_ausgenommen_Neuigkeiten_nicht()
    {
        var neuigkeit = await OeffentlichesBildAsync();
        int galerie;
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var eintrag = new GalerieEintrag { Titel = "Sommerfest", Öffentlich = true, DbImage = Bild() };
            kontext.Galerie.Add(eintrag);
            await kontext.SaveChangesAsync(Abbruch);
            galerie = eintrag.DbImage!.Id;
        }
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var client = app.CreateClient();

        var galerieAntwort = await client.GetAsync(Url(galerie), Abbruch);
        var kachelAntwort = await client.GetAsync(Url(galerie) + "?variante=galeriekachel", Abbruch);
        var neuigkeitAntwort = await client.GetAsync(Url(neuigkeit), Abbruch);

        galerieAntwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        galerieAntwort.Headers.GetValues("X-Robots-Tag").ShouldBe(["noindex"]);
        kachelAntwort.Headers.GetValues("X-Robots-Tag").ShouldBe(["noindex"]);
        neuigkeitAntwort.Headers.Contains("X-Robots-Tag").ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Unbekanntes_Bild_liefert_404_ohne_Cache_Header()
    {
        using var app = new TestWebAppFactory(Datenbank.Verbindung);
        using var admin = await AdminClientAsync(app);

        var antwort = await admin.GetAsync(Url(4711), Abbruch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        antwort.Headers.CacheControl.ShouldBeNull();
    }
}
