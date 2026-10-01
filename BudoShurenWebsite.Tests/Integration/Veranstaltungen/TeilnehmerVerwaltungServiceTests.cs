using System.Text;
using System.Text.RegularExpressions;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration.Veranstaltungen;

[Trait("Category", "Integration")]
public class TeilnehmerVerwaltungServiceTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Basis = "https://test.example/";
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _zeit = new(Start);
    private VerwaltungsBenutzer _admin = null!;
    private Veranstaltung _v = null!;

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private TeilnehmerVerwaltungService Service => new(
        new TestKontextFabrik(Datenbank),
        new AnmeldungMailVersand(new EmailWarteschlange(_zeit, new EmailVersandSignal())),
        _zeit,
        Options.Create(new VeranstaltungenOptionen { MaxInfoEmails = 5 }));

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        if (!TestDatenbank.Verfuegbar)
            return;

        await using var kontext = Datenbank.NeuerKontext();
        var user = new ApplicationUser { UserName = "orga", Vorname = "Olga", Name = "Organisatorin", Email = "orga@example.org" };
        kontext.Users.Add(user);
        kontext.Abteilungen.Add(new Abteilung { ID = "Aikido", Name = "Aikido" });
        _v = new Veranstaltung
        {
            Titel = "Herbstseminar",
            Slug = "herbstseminar",
            Status = VeranstaltungStatus.Veroeffentlicht,
            KontaktEmail = "seminar@example.org",
            MaxBegleitpersonen = 3,
            ErstelltUtc = Start.UtcDateTime,
            Tage =
            {
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = 5 },
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0), MaxTeilnehmer = 5 }
            }
        };
        kontext.Veranstaltungen.Add(_v);
        await kontext.SaveChangesAsync(Abbruch);
        _admin = new VerwaltungsBenutzer(user.Id, "Olga Organisatorin", IstAdmin: true, IstAbteilungsleiter: false, Abteilung: null);
    }

    private async Task<int> AnmeldungAnlegenAsync(string email, AnmeldungStatus status = AnmeldungStatus.Angemeldet, int begleitung = 0, string vorname = "Max")
    {
        await using var kontext = Datenbank.NeuerKontext();
        var a = new Anmeldung
        {
            VeranstaltungId = _v.Id,
            Email = email,
            Vorname = vorname,
            Nachname = "Muster",
            Status = status,
            AnzahlBegleitpersonen = begleitung,
            TokenHash = AnmeldeToken.Erzeugen().Hash,
            ErstelltUtc = Start.UtcDateTime,
            Ereignisse = { new AnmeldungEreignis { ZeitpunktUtc = Start.UtcDateTime, Akteur = EreignisAkteur.Teilnehmer, Art = AnmeldungEreignisArt.Angelegt } }
        };
        kontext.Anmeldungen.Add(a);
        await kontext.SaveChangesAsync(Abbruch);
        return a.Id;
    }

    private async Task<Anmeldung> LadenAsync(int id)
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Anmeldungen.AsNoTracking().Include(a => a.Ereignisse).SingleAsync(a => a.Id == id, Abbruch);
    }

    private async Task<List<EmailAusgang>> MailsAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.EmailAusgang.AsNoTracking().OrderBy(m => m.Id).ToListAsync(Abbruch);
    }

    private static AnmeldeEingabe Eingabe(string email, string vorname = "Max", int begleitung = 0) =>
        new() { Vorname = vorname, Nachname = "Muster", Email = email, AnzahlBegleitpersonen = begleitung };

    [DatenbankFact]
    public async Task Uebersicht_mit_Belegung_Kennzahlen_und_ungesehenen_Aenderungen()
    {
        var max = await AnmeldungAnlegenAsync("max@example.org", begleitung: 2);
        await AnmeldungAnlegenAsync("unbestaetigt@example.org", AnmeldungStatus.Unbestaetigt);
        await AnmeldungAnlegenAsync("weg@example.org", AnmeldungStatus.Storniert);
        await Service.AlsGesehenMarkierenAsync(_v.Id, [max], _admin, Abbruch);

        var u = (await Service.UebersichtAsync(_v.Id, _admin, Abbruch)).ShouldNotBeNull();

        u.Tage.Select(t => (t.Belegt, t.Max)).ShouldBe([(3, (int?)5), (3, (int?)5)], "unbestätigt ohne Reservierung belegt nichts");
        u.AktiveAnmeldungen.ShouldBe(2);
        u.BestaetigtePersonen.ShouldBe(3);
        u.Unbestaetigt.ShouldBe(1);
        u.UngeseheneAenderungen.ShouldBe(2);
        u.Teilnehmer.Single(t => t.Id == max).HatUngeseheneAenderungen.ShouldBeFalse();
        u.LetzteAenderungen.Count.ShouldBe(3);
    }

    [DatenbankFact]
    public async Task Fremde_Abteilung_sieht_nichts()
    {
        await AnmeldungAnlegenAsync("max@example.org");
        var leiter = new VerwaltungsBenutzer("x", "Leiter", IstAdmin: false, IstAbteilungsleiter: true, Abteilung: "Aikido");

        (await Service.UebersichtAsync(_v.Id, leiter, Abbruch)).ShouldBeNull("Gesamtverein nur für Admins");
        (await Service.CsvExportAsync(_v.Id, leiter, Abbruch)).ShouldBeNull();
        (await Service.NotizSpeichernAsync(_v.Id, 1, "x", leiter, Abbruch)).Erfolgreich.ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Ablehnen_mit_Grund_Mail_und_Historie()
    {
        var id = await AnmeldungAnlegenAsync("max@example.org");

        (await Service.AblehnenAsync(_v.Id, id, "  Nur für Mitglieder ", benachrichtigen: true, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var a = await LadenAsync(id);
        a.Status.ShouldBe(AnmeldungStatus.Abgelehnt);
        a.StatusGrund.ShouldBe("Nur für Mitglieder");
        var mail = (await MailsAsync()).ShouldHaveSingleItem();
        mail.Html.ShouldContain("Nur für Mitglieder");
        var detail = (await Service.DetailAsync(_v.Id, id, _admin, Abbruch)).ShouldNotBeNull();
        var ereignis = detail.Historie.First();
        ereignis.Text.ShouldBe("Abgelehnt: Nur für Mitglieder");
        ereignis.Akteur.ShouldBe(EreignisAkteur.Admin);
        ereignis.AkteurName.ShouldBe("Olga Organisatorin");
        ereignis.Ungesehen.ShouldBeFalse("eigene Aktionen gelten als gesehen");

        (await Service.AblehnenAsync(_v.Id, id, null, benachrichtigen: false, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("schon abgelehnt");
    }

    [DatenbankFact]
    public async Task Ablehnung_zuruecknehmen_nur_mit_Platz_und_mit_neuem_Link()
    {
        var id = await AnmeldungAnlegenAsync("max@example.org", AnmeldungStatus.Abgelehnt, begleitung: 1);
        var voll = await AnmeldungAnlegenAsync("voll@example.org", begleitung: 3); // 4 von 5 belegt

        (await Service.AblehnungZuruecknehmenAsync(_v.Id, id, Basis, _admin, Abbruch)).Fehler.ShouldHaveSingleItem().ShouldContain("Platz");

        await using (var kontext = Datenbank.NeuerKontext())
            await kontext.Anmeldungen.Where(a => a.Id == voll).ExecuteUpdateAsync(s => s.SetProperty(a => a.AnzahlBegleitpersonen, 0), Abbruch);
        (await Service.AblehnungZuruecknehmenAsync(_v.Id, id, Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var a = await LadenAsync(id);
        a.Status.ShouldBe(AnmeldungStatus.Angemeldet);
        a.Ereignisse.ShouldContain(e => e.Art == AnmeldungEreignisArt.AblehnungZurueckgenommen);
        var token = Regex.Match((await MailsAsync()).Single().Html, "meine-anmeldung/([A-Za-z0-9_-]{43})").Groups[1].Value;
        AnmeldeToken.Hash(token).ShouldBe(a.TokenHash);
    }

    [DatenbankFact]
    public async Task Manuell_anmelden()
    {
        var ergebnis = await Service.ManuellAnmeldenAsync(_v.Id, Eingabe(" Tel@Example.org ", begleitung: 1), benachrichtigen: true, Basis, _admin, Abbruch);

        ergebnis.Fehler.ShouldBeEmpty();
        var a = await LadenAsync(ergebnis.Id!.Value);
        a.Email.ShouldBe("tel@example.org");
        a.Status.ShouldBe(AnmeldungStatus.Angemeldet);
        a.Quelle.ShouldBe(AnmeldungQuelle.Admin);
        a.Ereignisse.ShouldHaveSingleItem().AkteurUserId.ShouldBe(_admin.UserId);
        (await MailsAsync()).ShouldHaveSingleItem().Betreff.ShouldStartWith("Anmeldung bestätigt");

        (await Service.ManuellAnmeldenAsync(_v.Id, Eingabe("tel@example.org"), false, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("schon angemeldet");
        (await Service.ManuellAnmeldenAsync(_v.Id, Eingabe("viele@example.org", begleitung: 3), false, Basis, _admin, Abbruch))
            .Fehler.ShouldHaveSingleItem().ShouldContain("Platz");
        (await Service.ManuellAnmeldenAsync(_v.Id, new AnmeldeEingabe { Email = "kaputt" }, false, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Manuell_anmelden_reaktiviert_eine_Abmeldung()
    {
        var id = await AnmeldungAnlegenAsync("max@example.org", AnmeldungStatus.Storniert);

        var ergebnis = await Service.ManuellAnmeldenAsync(_v.Id, Eingabe("max@example.org"), benachrichtigen: false, Basis, _admin, Abbruch);

        ergebnis.Id.ShouldBe(id);
        (await LadenAsync(id)).Status.ShouldBe(AnmeldungStatus.Angemeldet);
        (await MailsAsync()).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Bearbeiten_aendert_auch_die_Adresse_sofort_und_protokolliert_als_Organisator()
    {
        var id = await AnmeldungAnlegenAsync("max@example.org");
        await AnmeldungAnlegenAsync("andere@example.org");

        (await Service.BearbeitenAsync(_v.Id, id, Eingabe("andere@example.org"), false, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("Adresse vergeben");
        (await Service.BearbeitenAsync(_v.Id, id, Eingabe("max.muster@example.org", "Maximilian"), benachrichtigen: true, Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var a = await LadenAsync(id);
        a.Email.ShouldBe("max.muster@example.org");
        a.Vorname.ShouldBe("Maximilian");
        a.Ereignisse.Where(e => e.Akteur == EreignisAkteur.Admin).Select(e => e.Art)
            .ShouldBe([AnmeldungEreignisArt.DatenGeaendert, AnmeldungEreignisArt.EmailGeaendert], ignoreOrder: true);
        a.AdminGesehenUtc.ShouldBeNull("Organisator-Aktionen setzen nichts auf gesehen, sie zählen nur nicht als neu");
        (await MailsAsync()).ShouldHaveSingleItem().An.ShouldBe("max.muster@example.org");
    }

    [DatenbankFact]
    public async Task Notiz_und_Link_erneut_senden()
    {
        var id = await AnmeldungAnlegenAsync("max@example.org");
        var weg = await AnmeldungAnlegenAsync("weg@example.org", AnmeldungStatus.Storniert);

        (await Service.NotizSpeichernAsync(_v.Id, id, "Zahlt bar", _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await Service.NotizSpeichernAsync(_v.Id, id, new string('x', 2001), _admin, Abbruch)).Erfolgreich.ShouldBeFalse();
        (await LadenAsync(id)).AdminNotiz.ShouldBe("Zahlt bar");

        var vorher = (await LadenAsync(id)).TokenHash;
        (await Service.LinkErneutSendenAsync(_v.Id, id, Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await LadenAsync(id)).TokenHash.ShouldNotBe(vorher);
        (await MailsAsync()).ShouldHaveSingleItem().Html.ShouldContain($"{Basis}veranstaltungen/meine-anmeldung/");
        (await Service.LinkErneutSendenAsync(_v.Id, weg, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Csv_enthaelt_nur_aktive_Anmeldungen()
    {
        await AnmeldungAnlegenAsync("max@example.org", vorname: "Aktiv");
        await AnmeldungAnlegenAsync("weg@example.org", AnmeldungStatus.Storniert, vorname: "Storniert");

        var (name, inhalt) = (await Service.CsvExportAsync(_v.Id, _admin, Abbruch)).ShouldNotBeNull();

        name.ShouldBe("teilnehmer-herbstseminar-2026-10-01.csv");
        var text = Encoding.UTF8.GetString(inhalt);
        text.ShouldContain("\"Aktiv\"");
        text.ShouldNotContain("\"Storniert\"");
    }
}
