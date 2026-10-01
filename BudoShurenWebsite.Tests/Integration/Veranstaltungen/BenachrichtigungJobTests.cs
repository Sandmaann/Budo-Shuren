using System.Text.RegularExpressions;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration.Veranstaltungen;

[Trait("Category", "Integration")]
public class BenachrichtigungJobTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _zeit = new(Start);
    private string _orgaId = null!;
    private Veranstaltung _v = null!;

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private BenachrichtigungJob Job => new(
        new TestKontextFabrik(Datenbank),
        new EmailWarteschlange(_zeit, new EmailVersandSignal()),
        _zeit,
        Options.Create(new VeranstaltungenOptionen { WebsiteUrl = "https://test.example" }),
        NullLogger<BenachrichtigungJob>.Instance);

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        if (!TestDatenbank.Verfuegbar)
            return;

        await using var kontext = Datenbank.NeuerKontext();
        var orga = new ApplicationUser { UserName = "orga", Vorname = "Olga", Email = "orga@example.org" };
        kontext.Users.Add(orga);
        _v = new Veranstaltung
        {
            Titel = "Herbstseminar",
            Slug = "herbstseminar",
            Status = VeranstaltungStatus.Veroeffentlicht,
            KontaktEmail = "seminar@example.org",
            ErstelltUtc = Start.UtcDateTime,
            Tage =
            {
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = 3 }
            },
            BenachrichtigungEmpfaenger =
            {
                new BenachrichtigungEmpfaenger { User = orga, BenachrichtigtBisUtc = Start.UtcDateTime.AddHours(-1), ErstelltUtc = Start.UtcDateTime },
                new BenachrichtigungEmpfaenger { Email = "kasse@example.org", BenachrichtigtBisUtc = Start.UtcDateTime.AddHours(-1), ErstelltUtc = Start.UtcDateTime }
            }
        };
        kontext.Veranstaltungen.Add(_v);
        await kontext.SaveChangesAsync(Abbruch);
        _orgaId = orga.Id;
    }

    private async Task<int> AnmeldungAsync(string vorname, AnmeldungEreignisArt art, int begleitung = 0, string? akteurUserId = null,
        AnmeldungStatus status = AnmeldungStatus.Angemeldet, string? detailsJson = null, string? telefon = null)
    {
        await using var kontext = Datenbank.NeuerKontext();
        var a = new Anmeldung
        {
            VeranstaltungId = _v.Id,
            Email = $"{vorname.ToLowerInvariant()}@example.org",
            Vorname = vorname,
            Nachname = "Muster",
            TokenHash = AnmeldeToken.Erzeugen().Hash,
            Telefon = telefon,
            Status = status,
            AnzahlBegleitpersonen = begleitung,
            ErstelltUtc = _zeit.GetUtcNow().UtcDateTime,
            Ereignisse =
            {
                new AnmeldungEreignis
                {
                    ZeitpunktUtc = _zeit.GetUtcNow().UtcDateTime,
                    Art = art,
                    Akteur = akteurUserId is null ? EreignisAkteur.Teilnehmer : EreignisAkteur.Admin,
                    AkteurUserId = akteurUserId,
                    DetailsJson = detailsJson
                }
            }
        };
        kontext.Anmeldungen.Add(a);
        await kontext.SaveChangesAsync(Abbruch);
        return a.Id;
    }

    private async Task<List<EmailAusgang>> MailsAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.EmailAusgang.AsNoTracking().OrderBy(m => m.An).ToListAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Sofort_nach_5_Minuten_Ruhe_genau_einmal_an_Benutzer_und_freie_Adresse()
    {
        await AnmeldungAsync("Max", AnmeldungEreignisArt.Bestaetigt, begleitung: 1);

        _zeit.Advance(TimeSpan.FromMinutes(2));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(0, "noch keine 5 Minuten Ruhe");

        _zeit.Advance(TimeSpan.FromMinutes(4));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(2);
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(0, "jedes Ereignis nur einmal");

        var mails = await MailsAsync();
        mails.Select(m => m.An).ShouldBe(["kasse@example.org", "orga@example.org"]);
        mails.ShouldAllBe(m => m.Html.Contains("Max Muster") && m.Html.Contains("2 Personen") && m.Html.Contains("2 von 3 Plätzen belegt"));

        var orga = mails.Single(m => m.An == "orga@example.org");
        orga.Html.ShouldContain($"https://test.example/Account/Member/Veranstaltungen/{_v.Id}");
        orga.Html.ShouldNotContain("benachrichtigung-abmelden");

        var kasse = mails.Single(m => m.An == "kasse@example.org");
        kasse.Html.ShouldNotContain("Zur Übersicht");
        var token = Regex.Match(kasse.Html, "benachrichtigung-abmelden/([A-Za-z0-9_-]+)").Groups[1].Value;
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.BenachrichtigungEmpfaenger.SingleAsync(e => e.Email == "kasse@example.org", Abbruch))
            .AbmeldeTokenHash.ShouldBe(AnmeldeToken.Hash(token));
    }

    [DatenbankFact]
    public async Task Eigene_Aktion_wird_dem_Organisator_nicht_gemeldet()
    {
        await AnmeldungAsync("Max", AnmeldungEreignisArt.Angelegt, akteurUserId: _orgaId);

        _zeit.Advance(TimeSpan.FromMinutes(10));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(1);

        var mail = (await MailsAsync()).ShouldHaveSingleItem();
        mail.An.ShouldBe("kasse@example.org");
        mail.Html.ShouldContain("(durch Organisator)");
    }

    [DatenbankFact]
    public async Task Unbestaetigte_Anmeldung_wird_nicht_gemeldet()
    {
        await AnmeldungAsync("Max", AnmeldungEreignisArt.Angelegt, status: AnmeldungStatus.Unbestaetigt);

        _zeit.Advance(TimeSpan.FromMinutes(10));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Ausgebucht_und_datensparsam()
    {
        await AnmeldungAsync("Max", AnmeldungEreignisArt.Bestaetigt, begleitung: 2, telefon: "0170 999");
        await AnmeldungAsync("Erika", AnmeldungEreignisArt.DatenGeaendert,
            detailsJson: "{\"Telefon\":{\"Alt\":\"0170 111\",\"Neu\":\"0170 222\"},\"Bemerkung\":{\"Alt\":null,\"Neu\":\"Vegetarisch\"}}");

        _zeit.Advance(TimeSpan.FromMinutes(10));
        await Job.AusfuehrenAsync(Abbruch);

        var mail = (await MailsAsync()).First();
        mail.Betreff.ShouldBe("Benachrichtigung: Herbstseminar (ausgebucht)");
        mail.Html.ShouldContain("Erika Muster");
        mail.Html.ShouldNotContain("0170");
        mail.Html.ShouldNotContain("Vegetarisch");
    }

    [DatenbankFact]
    public async Task Zusammenfassung_kommt_nach_der_Uhrzeit()
    {
        await using (var kontext = Datenbank.NeuerKontext())
        {
            await kontext.BenachrichtigungEmpfaenger.Where(e => e.VeranstaltungId == _v.Id)
                .ExecuteUpdateAsync(e => e.SetProperty(x => x.Modus, BenachrichtigungModus.TaeglicheZusammenfassung), Abbruch);
        }
        await AnmeldungAsync("Max", AnmeldungEreignisArt.Bestaetigt);

        _zeit.Advance(TimeSpan.FromHours(1));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(0, "12:00 Ortszeit, die Zusammenfassung kommt um 07:00");

        // Nächster Morgen 07:05 Ortszeit = 05:05 UTC
        _zeit.SetUtcNow(new DateTimeOffset(2026, 10, 2, 5, 5, 0, TimeSpan.Zero));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(2);
        (await MailsAsync()).ShouldAllBe(m => m.Betreff == "Zusammenfassung: Herbstseminar");
    }

    [DatenbankFact]
    public async Task Benutzer_ohne_Adresse_wird_uebersprungen_und_nicht_erneut_versucht()
    {
        await using (var kontext = Datenbank.NeuerKontext())
        {
            await kontext.Users.Where(u => u.Id == _orgaId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Email, (string?)null), Abbruch);
        }
        await AnmeldungAsync("Max", AnmeldungEreignisArt.Bestaetigt);

        _zeit.Advance(TimeSpan.FromMinutes(10));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(1);

        await using var pruefung = Datenbank.NeuerKontext();
        (await pruefung.BenachrichtigungEmpfaenger.SingleAsync(e => e.UserId == _orgaId, Abbruch))
            .BenachrichtigtBisUtc.ShouldBe(_zeit.GetUtcNow().UtcDateTime - BenachrichtigungsAuswertung.Puffer);
    }

    [DatenbankFact]
    public async Task Nur_veroeffentlichte_Veranstaltungen()
    {
        await AnmeldungAsync("Max", AnmeldungEreignisArt.Bestaetigt);
        await using (var kontext = Datenbank.NeuerKontext())
        {
            await kontext.Veranstaltungen.ExecuteUpdateAsync(v => v.SetProperty(x => x.Status, VeranstaltungStatus.Abgesagt), Abbruch);
        }

        _zeit.Advance(TimeSpan.FromMinutes(10));
        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(0);
    }
}
