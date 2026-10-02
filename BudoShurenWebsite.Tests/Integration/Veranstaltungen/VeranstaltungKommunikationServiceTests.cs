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
public class VeranstaltungKommunikationServiceTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Basis = "https://test.example/";
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _zeit = new(Start);
    private VerwaltungsBenutzer _admin = null!;
    private Veranstaltung _v = null!;
    private int _samstag;
    private int _sonntag;

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private AnmeldungMailVersand Mails => new(new EmailWarteschlange(_zeit, new EmailVersandSignal()));

    private VeranstaltungKommunikationService Service => new(new TestKontextFabrik(Datenbank), Mails, _zeit);

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        if (!TestDatenbank.Verfuegbar)
            return;

        await using var kontext = Datenbank.NeuerKontext();
        var user = new ApplicationUser { UserName = "orga", Vorname = "Olga", Name = "Organisatorin" };
        kontext.Users.Add(user);
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
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0) },
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0) }
            }
        };
        kontext.Veranstaltungen.Add(_v);
        await kontext.SaveChangesAsync(Abbruch);
        _samstag = _v.Tage.Single(t => t.Datum.Day == 14).Id;
        _sonntag = _v.Tage.Single(t => t.Datum.Day == 15).Id;

        // Kalendereinträge wie nach dem Veröffentlichen
        foreach (var tag in _v.Tage)
        {
            var eintrag = new AppointmentData();
            KalenderEintragFabrik.Uebernehmen(eintrag, _v, tag, new DateTime(2026, 10, 1));
            kontext.Appointments.Add(eintrag);
        }
        await kontext.SaveChangesAsync(Abbruch);
        _admin = new VerwaltungsBenutzer(user.Id, "Olga Organisatorin", IstAdmin: true, IstAbteilungsleiter: false, Abteilung: null);
    }

    private async Task<int> AnmeldungAsync(string email, AnmeldungStatus status = AnmeldungStatus.Angemeldet, int[]? tage = null, params string[] infoEmails)
    {
        await using var kontext = Datenbank.NeuerKontext();
        var a = new Anmeldung
        {
            VeranstaltungId = _v.Id,
            Email = email,
            Vorname = "Max",
            Nachname = "Muster",
            Status = status,
            AnzahlBegleitpersonen = infoEmails.Length,
            TokenHash = AnmeldeToken.Erzeugen().Hash,
            ErstelltUtc = Start.UtcDateTime
        };
        foreach (var tagId in tage ?? [])
            a.Tage.Add(new AnmeldungTag { VeranstaltungsTagId = tagId });
        foreach (var info in infoEmails)
            a.InfoEmails.Add(new AnmeldungInfoEmail { Email = info, AbmeldeTokenHash = AnmeldeToken.Erzeugen().Hash });
        kontext.Anmeldungen.Add(a);
        await kontext.SaveChangesAsync(Abbruch);
        return a.Id;
    }

    private async Task<List<EmailAusgang>> MailsAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.EmailAusgang.AsNoTracking().OrderBy(m => m.Id).ToListAsync(Abbruch);
    }

    private async Task<T> MitKontextAsync<T>(Func<ApplicationDbContext, Task<T>> abfrage)
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await abfrage(kontext);
    }

    [DatenbankFact]
    public async Task Rundmail_an_bestaetigte_Teilnehmer_und_auf_Wunsch_an_Info_Adressen()
    {
        await AnmeldungAsync("max@example.org", infoEmails: ["begleitung@example.org", "doppelt@example.org"]);
        await AnmeldungAsync("doppelt@example.org");
        await AnmeldungAsync("unbestaetigt@example.org", AnmeldungStatus.Unbestaetigt);
        await AnmeldungAsync("weg@example.org", AnmeldungStatus.Storniert);

        var ergebnis = await Service.RundmailSendenAsync(_v.Id, "Treffpunkt", "**Wichtig:** Halle 2 <script>x</script>", anInfoAdressen: true, Basis, _admin, Abbruch);

        ergebnis.Fehler.ShouldBeEmpty();
        var mails = await MailsAsync();
        mails.Select(m => m.An).ShouldBe(["max@example.org", "doppelt@example.org", "begleitung@example.org"], ignoreOrder: true, "jede Adresse höchstens einmal");
        mails.ShouldAllBe(m => m.Betreff == "Treffpunkt" && m.AntwortAn == "seminar@example.org");
        var teilnehmerMail = mails.Single(m => m.An == "max@example.org");
        teilnehmerMail.Html.ShouldContain("<strong>Wichtig:</strong>");
        teilnehmerMail.Html.ShouldNotContain("<script>");
        teilnehmerMail.Html.ShouldContain($"{Basis}veranstaltungen/link-anfordern");
        var infoToken = Regex.Match(mails.Single(m => m.An == "begleitung@example.org").Html, "info-abmelden/([A-Za-z0-9_-]{43})").Groups[1].Value;
        var info = await MitKontextAsync(k => k.AnmeldungInfoEmails.SingleAsync(i => i.Email == "begleitung@example.org", Abbruch));
        AnmeldeToken.Hash(infoToken).ShouldBe(info.AbmeldeTokenHash);

        var protokoll = (await Service.NachrichtenAsync(_v.Id, _admin, Abbruch)).ShouldHaveSingleItem();
        protokoll.Art.ShouldBe(NachrichtArt.Rundmail);
        protokoll.AnzahlEmpfaenger.ShouldBe(3);
        protokoll.Absender.ShouldBe("Olga Organisatorin");
    }

    [DatenbankFact]
    public async Task Rundmail_ohne_Info_Adressen_und_Fehlerfaelle()
    {
        (await Service.RundmailSendenAsync(_v.Id, "Info", "Text", false, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("noch keine Teilnehmer");
        await AnmeldungAsync("max@example.org", infoEmails: "begleitung@example.org");

        (await Service.RundmailSendenAsync(_v.Id, " ", "Text", false, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse();
        (await Service.RundmailSendenAsync(_v.Id, "Info", " ", false, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse();
        (await Service.RundmailSendenAsync(_v.Id, "Info", "Text", false, Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await MailsAsync()).Select(m => m.An).ShouldBe(["max@example.org"]);
        var fremd = new VerwaltungsBenutzer("x", "Editor", false, false, "Aikido"); // ohne Verwaltungsrechte
        (await Service.RundmailSendenAsync(_v.Id, "Info", "Text", false, Basis, fremd, Abbruch)).Erfolgreich.ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Testmail_geht_nur_an_die_angegebene_Adresse_und_wird_nicht_protokolliert()
    {
        await AnmeldungAsync("max@example.org");

        (await Service.TestmailSendenAsync(_v.Id, "Treffpunkt", "Halle 2", "Orga@Example.org", Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var mail = (await MailsAsync()).ShouldHaveSingleItem();
        mail.An.ShouldBe("orga@example.org");
        mail.Betreff.ShouldBe("[Test] Treffpunkt");
        (await Service.NachrichtenAsync(_v.Id, _admin, Abbruch)).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Tag_absagen_informiert_die_Betroffenen_und_entfernt_den_Kalendereintrag()
    {
        await AnmeldungAsync("max@example.org", infoEmails: "begleitung@example.org");
        await AnmeldungAsync("unbestaetigt@example.org", AnmeldungStatus.Unbestaetigt);
        await AnmeldungAsync("weg@example.org", AnmeldungStatus.Storniert);

        (await Service.TagAbsagenAsync(_v.Id, _samstag, "Die Halle ist **gesperrt**.", Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await MitKontextAsync(k => k.VeranstaltungsTage.SingleAsync(t => t.Id == _samstag, Abbruch))).Abgesagt.ShouldBeTrue();
        (await MitKontextAsync(k => k.Appointments.Select(a => a.VeranstaltungsTagId).ToListAsync(Abbruch))).ShouldBe([_sonntag]);
        var mails = await MailsAsync();
        mails.Select(m => m.An).ShouldBe(["max@example.org", "unbestaetigt@example.org", "begleitung@example.org"], ignoreOrder: true);
        mails.First().Html.ShouldContain("Samstag, 14. November 2026");
        mails.First().Html.ShouldContain("<strong>gesperrt</strong>");
        var ereignisse = await MitKontextAsync(k => k.AnmeldungEreignisse.Where(e => e.Art == AnmeldungEreignisArt.TagAbgesagt).ToListAsync(Abbruch));
        ereignisse.Count.ShouldBe(2, "nur aktive Anmeldungen");
        EreignisText.Beschreiben(ereignisse[0].Art, ereignisse[0].DetailsJson).ShouldBe("Termin abgesagt: Sa 14.11.");

        (await Service.TagAbsagenAsync(_v.Id, _samstag, null, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("schon abgesagt");
        (await Service.TagAbsagenAsync(_v.Id, _sonntag, null, Basis, _admin, Abbruch)).Fehler.ShouldHaveSingleItem().ShouldContain("ganze Veranstaltung");
    }

    [DatenbankFact]
    public async Task Bei_Teilanmeldung_sind_nur_Anmeldungen_fuer_diesen_Tag_betroffen()
    {
        await using (var kontext = Datenbank.NeuerKontext())
            await kontext.Veranstaltungen.Where(v => v.Id == _v.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.Teilnahmemodus, Teilnahmemodus.EinzelneTage), Abbruch);
        await AnmeldungAsync("samstag@example.org", tage: [_samstag]);
        await AnmeldungAsync("sonntag@example.org", tage: [_sonntag]);

        (await Service.TagAbsagenAsync(_v.Id, _samstag, null, Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await MailsAsync()).Select(m => m.An).ShouldBe(["samstag@example.org"]);
    }

    [DatenbankFact]
    public async Task Veranstaltung_absagen()
    {
        await AnmeldungAsync("max@example.org", infoEmails: "begleitung@example.org");
        await AnmeldungAsync("weg@example.org", AnmeldungStatus.Storniert);

        (await Service.AbsagenAsync(_v.Id, null, Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await MitKontextAsync(k => k.Veranstaltungen.SingleAsync(Abbruch))).Status.ShouldBe(VeranstaltungStatus.Abgesagt);
        (await MitKontextAsync(k => k.Appointments.CountAsync(Abbruch))).ShouldBe(0);
        var mails = await MailsAsync();
        mails.Select(m => m.An).ShouldBe(["max@example.org", "begleitung@example.org"], ignoreOrder: true);
        mails.ShouldAllBe(m => m.Betreff == "Abgesagt: Herbstseminar");
        (await Service.NachrichtenAsync(_v.Id, _admin, Abbruch)).ShouldHaveSingleItem().Art.ShouldBe(NachrichtArt.VeranstaltungAbgesagt);

        (await Service.AbsagenAsync(_v.Id, null, Basis, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("schon abgesagt");
    }

    [DatenbankFact]
    public async Task Organisator_Aktionen_verdecken_keine_ungesehenen_Aenderungen_der_Teilnehmer()
    {
        var id = await AnmeldungAsync("max@example.org");
        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.AnmeldungEreignisse.Add(new AnmeldungEreignis
            {
                AnmeldungId = id, ZeitpunktUtc = Start.UtcDateTime, Akteur = EreignisAkteur.Teilnehmer, Art = AnmeldungEreignisArt.BegleitungGeaendert
            });
            await kontext.SaveChangesAsync(Abbruch);
        }
        _zeit.Advance(TimeSpan.FromMinutes(5));

        (await Service.TagAbsagenAsync(_v.Id, _samstag, null, Basis, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var verwaltung = new TeilnehmerVerwaltungService(new TestKontextFabrik(Datenbank), Mails, _zeit, Options.Create(new VeranstaltungenOptionen()));
        var uebersicht = (await verwaltung.UebersichtAsync(_v.Id, _admin, Abbruch))!;
        uebersicht.UngeseheneAenderungen.ShouldBe(1);
        uebersicht.LetzteAenderungen.Single(e => e.Art == AnmeldungEreignisArt.TagAbgesagt).Ungesehen.ShouldBeFalse();
        uebersicht.LetzteAenderungen.Single(e => e.Art == AnmeldungEreignisArt.BegleitungGeaendert).Ungesehen.ShouldBeTrue();
    }
}
