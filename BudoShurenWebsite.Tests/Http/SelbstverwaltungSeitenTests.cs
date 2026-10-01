using System.Net;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Http;

/// <summary>Self-Service-Seiten über die komplette App: Vorbelegung, Ändern, Abmelden, E-Mail-Wechsel, Links, Info-Abmeldung.</summary>
[Trait("Category", "Integration")]
public class SelbstverwaltungSeitenTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _zeit = new(Start);

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private TestWebAppFactory App() => new(Datenbank.Verbindung, veranstaltungenAktiviert: true, _zeit);

    private async Task<(Veranstaltung Veranstaltung, AnmeldeTokenPaar Link, AnmeldeTokenPaar InfoLink)> AngemeldetAsync()
    {
        var link = AnmeldeToken.Erzeugen();
        var infoLink = AnmeldeToken.Erzeugen();
        var v = new Veranstaltung
        {
            Titel = "Herbstseminar",
            Slug = "herbstseminar",
            Status = VeranstaltungStatus.Veroeffentlicht,
            KontaktEmail = "seminar@example.org",
            MaxBegleitpersonen = 3,
            ErstelltUtc = Start.UtcDateTime,
            Tage = { new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = 20 } }
        };
        await using var kontext = Datenbank.NeuerKontext();
        kontext.Veranstaltungen.Add(v);
        kontext.Anmeldungen.Add(new Anmeldung
        {
            Veranstaltung = v,
            Email = "max@example.org",
            Vorname = "Max",
            Nachname = "Muster",
            AnzahlBegleitpersonen = 1,
            Status = AnmeldungStatus.Angemeldet,
            TokenHash = link.Hash,
            ErstelltUtc = Start.UtcDateTime,
            InfoEmails = { new AnmeldungInfoEmail { Email = "begleitung@example.org", AbmeldeTokenHash = infoLink.Hash } }
        });
        await kontext.SaveChangesAsync(Abbruch);
        return (v, link, infoLink);
    }

    private async Task<Anmeldung> AnmeldungAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Anmeldungen.AsNoTracking().Include(a => a.InfoEmails).SingleAsync(Abbruch);
    }

    /// <summary>
    /// Schickt das Formular ab und liefert das HTML dekodiert: Blazor schreibt Umlaute aus Ausdrücken (z. B. Meldungen)
    /// als Entities (&amp;#xC4;), fester Text im Markup bleibt unverändert.
    /// </summary>
    private async Task<string> AbschickenAsync(HttpClient client, string url, Dictionary<string, string> felder)
    {
        var antwort = await client.PostAsync(url, new FormUrlEncodedContent(felder), Abbruch);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return WebUtility.HtmlDecode(await antwort.Content.ReadAsStringAsync(Abbruch));
    }

    [DatenbankFact]
    public async Task Meine_Anmeldung_zeigt_die_Daten_vorbelegt_und_sendet_Sicherheits_Header()
    {
        var (_, link, _) = await AngemeldetAsync();
        await using var app = App();
        using var client = app.CreateClient();

        var antwort = await client.GetAsync($"/veranstaltungen/meine-anmeldung/{link.Klartext}", Abbruch);

        antwort.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        var felder = HtmlFormular.AlleFelder(await antwort.Content.ReadAsStringAsync(Abbruch), "aendern");
        felder["Aenderung.Eingabe.Vorname"].ShouldBe("Max");
        felder["Aenderung.Eingabe.Email"].ShouldBe("max@example.org");
        felder["Aenderung.Eingabe.AnzahlBegleitpersonen"].ShouldBe("1");
        felder["Aenderung.Eingabe.InfoEmails"].ShouldBe("begleitung@example.org");
    }

    [DatenbankFact]
    public async Task Ungueltiger_Link_verweist_auf_Link_anfordern()
    {
        await AngemeldetAsync();
        await using var app = App();
        using var client = app.CreateClient();

        var html = await client.GetStringAsync($"/veranstaltungen/meine-anmeldung/{AnmeldeToken.Erzeugen().Klartext}", Abbruch);

        html.ShouldContain("Link ungültig");
        html.ShouldContain("/veranstaltungen/link-anfordern");
    }

    [DatenbankFact]
    public async Task Aendern_ueber_das_Formular()
    {
        var (_, link, _) = await AngemeldetAsync();
        await using var app = App();
        using var client = app.CreateClient();
        var url = $"/veranstaltungen/meine-anmeldung/{link.Klartext}";
        var felder = HtmlFormular.AlleFelder(await client.GetStringAsync(url, Abbruch), "aendern");
        felder["Aenderung.Eingabe.Vorname"] = "Moritz";
        felder["Aenderung.Eingabe.AnzahlBegleitpersonen"] = "2";

        var html = await AbschickenAsync(client, url, felder);

        html.ShouldContain("Deine Änderungen sind gespeichert");
        var a = await AnmeldungAsync();
        a.Vorname.ShouldBe("Moritz");
        a.AnzahlBegleitpersonen.ShouldBe(2);
        a.InfoEmails.Single().Email.ShouldBe("begleitung@example.org", "unveränderte Info-Adresse bleibt");
    }

    [DatenbankFact]
    public async Task Fehler_beim_Aendern_werden_am_Feld_gezeigt_und_nichts_gespeichert()
    {
        var (_, link, _) = await AngemeldetAsync();
        await using var app = App();
        using var client = app.CreateClient();
        var url = $"/veranstaltungen/meine-anmeldung/{link.Klartext}";
        var felder = HtmlFormular.AlleFelder(await client.GetStringAsync(url, Abbruch), "aendern");
        felder["Aenderung.Eingabe.Nachname"] = "";

        var html = await AbschickenAsync(client, url, felder);

        html.ShouldContain("Bitte gib deinen Nachnamen an.");
        (await AnmeldungAsync()).Nachname.ShouldBe("Muster");
    }

    [DatenbankFact]
    public async Task Abmelden_nur_mit_Haken()
    {
        var (_, link, _) = await AngemeldetAsync();
        await using var app = App();
        using var client = app.CreateClient();
        var url = $"/veranstaltungen/meine-anmeldung/{link.Klartext}";
        var seite = await client.GetStringAsync(url, Abbruch);

        var ohneHaken = await AbschickenAsync(client, url, HtmlFormular.VersteckteFelder(seite, "abmelden"));
        ohneHaken.ShouldContain("Bitte setze den Haken");
        (await AnmeldungAsync()).Status.ShouldBe(AnmeldungStatus.Angemeldet);

        var felder = HtmlFormular.VersteckteFelder(seite, "abmelden");
        felder["Abmeldung.Bestaetigt"] = "true";
        var mitHaken = await AbschickenAsync(client, url, felder);

        mitHaken.ShouldContain("Du bist abgemeldet");
        (await AnmeldungAsync()).Status.ShouldBe(AnmeldungStatus.Storniert);
    }

    [DatenbankFact]
    public async Task Neue_E_Mail_Adresse_erst_nach_Klick_auf_den_Link()
    {
        var (_, link, _) = await AngemeldetAsync();
        var wechsel = AnmeldeToken.Erzeugen();
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var a = await kontext.Anmeldungen.SingleAsync(Abbruch);
            a.NeueEmail = "neu@example.org";
            a.NeueEmailTokenHash = wechsel.Hash;
            await kontext.SaveChangesAsync(Abbruch);
        }
        await using var app = App();
        using var client = app.CreateClient();
        var url = $"/veranstaltungen/email-bestaetigen/{wechsel.Klartext}";

        var seite = await client.GetStringAsync(url, Abbruch);
        seite.ShouldContain("neu@example.org");
        (await AnmeldungAsync()).Email.ShouldBe("max@example.org", "Öffnen ändert nichts");

        (await AbschickenAsync(client, url, HtmlFormular.VersteckteFelder(seite, "email-bestaetigen"))).ShouldContain("Neue Adresse bestätigt");
        (await AnmeldungAsync()).Email.ShouldBe("neu@example.org");
    }

    [DatenbankFact]
    public async Task Link_anfordern_antwortet_immer_gleich()
    {
        await AngemeldetAsync();
        await using var app = App();
        using var client = app.CreateClient();

        async Task<string> AnfordernAsync(string email)
        {
            var felder = HtmlFormular.VersteckteFelder(await client.GetStringAsync("/veranstaltungen/link-anfordern", Abbruch), "link-anfordern");
            felder["Formular.Email"] = email;
            felder["Formular.Website"] = "";
            _zeit.Advance(TimeSpan.FromSeconds(10));
            return await AbschickenAsync(client, "/veranstaltungen/link-anfordern", felder);
        }

        var bekannt = await AnfordernAsync("max@example.org");
        var unbekannt = await AnfordernAsync("unbekannt@example.org");

        bekannt.ShouldContain("Schau in dein Postfach");
        unbekannt.ShouldContain("Schau in dein Postfach");
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.EmailAusgang.Select(m => m.An).ToListAsync(Abbruch)).ShouldBe(["max@example.org"]);
    }

    [DatenbankFact]
    public async Task Info_Adresse_abmelden_erst_per_Klick()
    {
        var (_, _, infoLink) = await AngemeldetAsync();
        await using var app = App();
        using var client = app.CreateClient();
        var url = $"/veranstaltungen/info-abmelden/{infoLink.Klartext}";

        var seite = await client.GetStringAsync(url, Abbruch);
        seite.ShouldContain("Herbstseminar");
        (await AnmeldungAsync()).InfoEmails.Single().AbgemeldetUtc.ShouldBeNull("Öffnen ändert nichts");

        (await AbschickenAsync(client, url, HtmlFormular.VersteckteFelder(seite, "info-abmelden"))).ShouldContain("Du bekommst keine Infos mehr");
        (await AnmeldungAsync()).InfoEmails.Single().AbgemeldetUtc.ShouldNotBeNull();
    }
}
