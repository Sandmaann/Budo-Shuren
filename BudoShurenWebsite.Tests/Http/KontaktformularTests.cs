using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>
/// Kontaktformular (InteresseFormular) als Formular-POST auf einer statisch gerenderten Seite, so wie es
/// ein Browser ohne JavaScript oder ein Skript abschickt: Versand, Spam-Schutz und Protokoll.
/// </summary>
[Trait("Category", "Integration")]
public class KontaktformularTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Seite = "/Bujinkan";
    private const string Absender = "interessent@example.org";
    private const string Erfolg = "Deine Nachricht wurde erfolgreich versendet!";
    private const string Fehlschlag = "konnte leider nicht versendet werden";

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private TestWebAppFactory App() => new(Datenbank.Verbindung, zeit: _zeit);

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        if (!TestDatenbank.Verfuegbar)
            return;

        await using var kontext = Datenbank.NeuerKontext();
        kontext.Abteilungen.Add(new Abteilung { ID = "Bujinkan", Name = "Bujinkan", Abteilungsleiter = "Hanzo Hattori", Email = "bujinkan@example.org" });
        await kontext.SaveChangesAsync(Abbruch);
    }

    /// <summary>Lädt die Seite, füllt das Formular aus und schickt es nach <paramref name="wartezeit"/> ab.</summary>
    private async Task<string> AbschickenAsync(HttpClient client, string nachricht, TimeSpan? wartezeit = null, Action<Dictionary<string, string>>? aendern = null)
    {
        var seite = await client.GetStringAsync(Seite, Abbruch);
        var felder = HtmlFormular.VersteckteFelder(seite, "nachricht");
        felder["Input.Email"] = Absender;
        felder["Input.Text"] = nachricht;
        felder["Input.Website"] = "";
        aendern?.Invoke(felder);
        _zeit.Advance(wartezeit ?? TimeSpan.FromSeconds(30));

        var antwort = await client.PostAsync(Seite, new FormUrlEncodedContent(felder), Abbruch);
        return await antwort.Content.ReadAsStringAsync(Abbruch);
    }

    private async Task<KontaktAnfrage> EinzigerEintragAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return (await kontext.KontaktAnfragen.AsNoTracking().ToListAsync(Abbruch)).ShouldHaveSingleItem();
    }

    [DatenbankFact]
    public async Task Nachricht_geht_an_Abteilung_und_Abteilungsleiter_und_steht_im_Protokoll()
    {
        await using var app = App();
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var leiter = new ApplicationUser { UserName = "leiter", Email = "leiter@example.org", Vorname = "Lea", Abteilung = "Bujinkan", Verified = true };
            (await userManager.CreateAsync(leiter)).Succeeded.ShouldBeTrue();
            (await userManager.AddToRoleAsync(leiter, Roles.Abteilungsleiter)).Succeeded.ShouldBeTrue();
        }
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Hallo, wann ist das Probetraining?");

        html.ShouldContain(Erfolg);
        app.Mails.Gesendet.Select(m => m.To.ToString()).ShouldBe(["bujinkan@example.org", "leiter@example.org", Absender], ignoreOrder: true);

        var eintrag = await EinzigerEintragAsync();
        eintrag.Status.ShouldBe(KontaktAnfrageStatus.Zugestellt);
        eintrag.Abteilung.ShouldBe("Bujinkan");
        eintrag.AbsenderEmail.ShouldBe(Absender);
        eintrag.Nachricht.ShouldBe("Hallo, wann ist das Probetraining?");
        eintrag.AnzahlMails.ShouldBe(2, "die Bestätigung an den Absender zählt nicht als Benachrichtigung der Abteilung");
        eintrag.Seite.ShouldBe("Bujinkan");
        eintrag.Interaktiv.ShouldBeFalse();
        eintrag.ZeitpunktUtc.ShouldBe(_zeit.GetUtcNow().UtcDateTime);

        // Der Besucherzähler zählt die Nachricht, aber nicht unter der E-Mail-Adresse: Besuche werden nie gelöscht
        await using var kontext = Datenbank.NeuerKontext();
        var besuch = (await kontext.Visits.AsNoTracking().Where(v => v.PageName == "InteresseEmail_Bujinkan").ToListAsync(Abbruch)).ShouldHaveSingleItem();
        besuch.VisitorID.ShouldNotBe(Absender);
        (await kontext.Visits.AnyAsync(v => v.VisitorID == Absender, Abbruch)).ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task HTML_aus_dem_Formular_landet_nur_als_Text_in_den_Mails()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Erste Zeile\n<img src=\"https://spam.example/x.png\"> & <b>fett</b>");

        html.ShouldContain(Erfolg);
        var anAbteilung = app.Mails.Gesendet.Single(m => m.To.ToString() == "bujinkan@example.org");
        anAbteilung.HtmlBody.ShouldNotContain("<img");
        anAbteilung.HtmlBody.ShouldNotContain("<b>");
        anAbteilung.HtmlBody.ShouldContain("Erste Zeile<br>&lt;img src=&quot;https://spam.example/x.png&quot;&gt; &amp; &lt;b&gt;fett&lt;/b&gt;");
    }

    [DatenbankFact]
    public async Task Bestaetigung_an_den_Absender_enthaelt_den_Text_der_Nachricht_nicht()
    {
        // Die Adresse ist ungeprüft: sonst ließe sich über das Formular beliebiger Text an Fremde schicken
        await using var app = App();
        using var client = app.CreateClient();

        await AbschickenAsync(client, "Einmaliges Geheimwort Kirschbluete");

        var bestaetigung = app.Mails.Gesendet.Single(m => m.To.ToString() == Absender);
        bestaetigung.HtmlBody.ShouldContain("Vielen Dank");
        bestaetigung.HtmlBody.ShouldNotContain("Kirschbluete");
        bestaetigung.HtmlBody.ShouldNotContain("[NACHRICHT]");
        app.Mails.Gesendet.Single(m => m.To.ToString() == "bujinkan@example.org").HtmlBody.ShouldContain("Kirschbluete");
    }

    [DatenbankFact]
    public async Task Absender_Adresse_mit_HTML_ist_in_der_Mail_nur_Text_und_die_Nachricht_kommt_trotzdem_an()
    {
        // [EmailAddress] lässt solche Adressen durch. Die Bestätigung an den Absender scheitert daran, die Nachricht nicht.
        await using var app = App();
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Hallo", aendern: f => f["Input.Email"] = "a\"><b>@example.org");

        html.ShouldContain(Erfolg);
        var mail = app.Mails.Gesendet.ShouldHaveSingleItem();
        mail.To.ToString().ShouldBe("bujinkan@example.org");
        mail.HtmlBody.ShouldContain("mailto:a&quot;&gt;&lt;b&gt;@example.org");
        mail.HtmlBody.ShouldNotContain("<b>");

        var eintrag = await EinzigerEintragAsync();
        eintrag.Status.ShouldBe(KontaktAnfrageStatus.Zugestellt);
        eintrag.AnzahlMails.ShouldBe(1);
        eintrag.Hinweis.ShouldNotBeNull().ShouldContain("Bestätigung");
    }

    [DatenbankFact]
    public async Task Ausgefuellter_Honeypot_wird_abgelehnt_und_protokolliert()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Günstige Angebote", aendern: f => f["Input.Website"] = "https://spam.example");

        html.ShouldContain(Fehlschlag);
        html.ShouldNotContain(Erfolg);
        app.Mails.Gesendet.ShouldBeEmpty();
        var eintrag = await EinzigerEintragAsync();
        eintrag.Status.ShouldBe(KontaktAnfrageStatus.Honeypot);
        eintrag.AnzahlMails.ShouldBe(0);
        eintrag.Nachricht.ShouldBe("Günstige Angebote");
    }

    [DatenbankFact]
    public async Task Zu_schnelles_Absenden_wird_abgelehnt_und_klappt_nach_kurzem_Warten()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Hallo", wartezeit: TimeSpan.FromSeconds(1));

        html.ShouldContain("Das ging sehr schnell");
        app.Mails.Gesendet.ShouldBeEmpty();
        (await EinzigerEintragAsync()).Status.ShouldBe(KontaktAnfrageStatus.ZuSchnell);

        // Das zurückgegebene Formular behält Eingaben und Zeitstempel: nach ein paar Sekunden geht es
        var felder = HtmlFormular.AlleFelder(html, "nachricht");
        felder["Input.Text"].ShouldBe("Hallo");
        _zeit.Advance(TimeSpan.FromSeconds(5));
        var zweiterVersuch = await client.PostAsync(Seite, new FormUrlEncodedContent(felder), Abbruch);

        (await zweiterVersuch.Content.ReadAsStringAsync(Abbruch)).ShouldContain(Erfolg);
    }

    [DatenbankFact]
    public async Task Ohne_Zeitstempel_des_ausgelieferten_Formulars_wird_abgelehnt()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Hallo", aendern: f => f.Remove("Input.Zeitstempel"));

        html.ShouldNotContain(Erfolg);
        app.Mails.Gesendet.ShouldBeEmpty();
        (await EinzigerEintragAsync()).Status.IstAbgelehnt().ShouldBeTrue();
    }

    [DatenbankFact]
    public async Task Abgelaufenes_Formular_wird_abgelehnt_und_bekommt_einen_neuen_Zeitstempel()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Hallo", wartezeit: TimeSpan.FromHours(25));

        html.ShouldContain("Das Formular war zu lange");
        app.Mails.Gesendet.ShouldBeEmpty();
        (await EinzigerEintragAsync()).Status.ShouldBe(KontaktAnfrageStatus.FormularUngueltig);

        var felder = HtmlFormular.AlleFelder(html, "nachricht");
        _zeit.Advance(TimeSpan.FromSeconds(5));
        var zweiterVersuch = await client.PostAsync(Seite, new FormUrlEncodedContent(felder), Abbruch);

        (await zweiterVersuch.Content.ReadAsStringAsync(Abbruch)).ShouldContain(Erfolg);
    }

    [DatenbankFact]
    public async Task Werbung_mit_mehreren_Links_wird_abgelehnt()
    {
        await using var app = App();
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Mehr Besucher für Ihre Seite: https://a.example und https://b.example");

        html.ShouldContain("Bitte entferne die Links");
        app.Mails.Gesendet.ShouldBeEmpty();
        var eintrag = await EinzigerEintragAsync();
        eintrag.Status.ShouldBe(KontaktAnfrageStatus.Inhalt);
        eintrag.Hinweis.ShouldBe("2 Links");
    }

    [DatenbankFact]
    public async Task Versandfehler_wird_gemeldet_und_protokolliert()
    {
        await using var app = App();
        app.Mails.FehlerBeimVerbinden = new InvalidOperationException("SMTP nicht erreichbar");
        using var client = app.CreateClient();

        var html = await AbschickenAsync(client, "Hallo");

        html.ShouldContain(Fehlschlag);
        html.ShouldNotContain(Erfolg);
        var eintrag = await EinzigerEintragAsync();
        eintrag.Status.ShouldBe(KontaktAnfrageStatus.VersandFehler);
        eintrag.AnzahlMails.ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Admin_Seite_zeigt_Zahlen_und_Verlauf()
    {
        await using var app = App();
        using var gast = app.CreateClient();
        await AbschickenAsync(gast, "Hallo, wann ist das Probetraining?");

        string adminId;
        using (var scope = app.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { UserName = "admin", Email = "admin@example.org", Vorname = "Admin", Verified = true, EmailConfirmed = true };
            (await userManager.CreateAsync(admin)).Succeeded.ShouldBeTrue();
            (await userManager.AddToRoleAsync(admin, Roles.Admin)).Succeeded.ShouldBeTrue();
            adminId = admin.Id;
        }
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(TestAnmeldung.Header, adminId);

        var html = await client.GetStringAsync("/Account/Member/Admin", Abbruch);

        html.ShouldContain("Nachrichten nach Abteilung");
        html.ShouldContain(Absender);
        html.ShouldContain("Hallo, wann ist das Probetraining?");
        html.ShouldContain("Formular-POST");
    }
}
