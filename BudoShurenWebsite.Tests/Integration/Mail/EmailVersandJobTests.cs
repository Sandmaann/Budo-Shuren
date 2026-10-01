using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Mail;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration.Mail;

[Trait("Category", "Integration")]
public class EmailVersandJobTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _zeit = new(Start);
    private readonly FakeMailTransport _transport = new();

    private EmailVersandJob NeuerJob(int stapelGroesse = 50, int maxVersuche = 5) => new(
        new TestKontextFabrik(Datenbank),
        _transport,
        _zeit,
        Options.Create(new EmailVersandOptionen
        {
            StapelGroesse = stapelGroesse,
            MaxVersuche = maxVersuche,
            PauseZwischenMails = TimeSpan.Zero
        }),
        NullLogger<EmailVersandJob>.Instance);

    private async Task EinreihenAsync(params AusgehendeEmail[] mails)
    {
        var warteschlange = new EmailWarteschlange(_zeit, new EmailVersandSignal());
        await using var kontext = Datenbank.NeuerKontext();
        foreach (var mail in mails)
            warteschlange.Hinzufuegen(kontext, mail);
        await kontext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<EmailAusgang>> AlleEintraegeAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.EmailAusgang.AsNoTracking().OrderBy(m => m.Id).ToListAsync(TestContext.Current.CancellationToken);
    }

    private static AusgehendeEmail Mail(string an, EmailPrioritaet prioritaet = EmailPrioritaet.Normal) =>
        new(an, $"Betreff für {an}", "<p>Text</p>") { Prioritaet = prioritaet };

    [DatenbankFact]
    public async Task Faellige_Mails_werden_ueber_eine_Verbindung_versendet_und_markiert()
    {
        await EinreihenAsync(Mail("a@example.org"), Mail("b@example.org"));

        var bearbeitet = await NeuerJob().StapelVersendenAsync(TestContext.Current.CancellationToken);

        bearbeitet.ShouldBe(2);
        _transport.Verbindungen.ShouldBe(1);
        _transport.Gesendet.Select(m => m.To.Mailboxes.Single().Address).ShouldBe(["a@example.org", "b@example.org"]);
        var eintraege = await AlleEintraegeAsync();
        eintraege.ShouldAllBe(m => m.Status == EmailStatus.Gesendet && m.GesendetAmUtc == Start.UtcDateTime);
    }

    [DatenbankFact]
    public async Task Ohne_faellige_Mails_wird_keine_Verbindung_geoeffnet()
    {
        var bearbeitet = await NeuerJob().StapelVersendenAsync(TestContext.Current.CancellationToken);

        bearbeitet.ShouldBe(0);
        _transport.Verbindungen.ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Hohe_Prioritaet_zuerst_und_hoechstens_ein_Stapel()
    {
        await EinreihenAsync(
            Mail("normal1@example.org"),
            Mail("normal2@example.org"),
            Mail("hoch@example.org", EmailPrioritaet.Hoch));

        var bearbeitet = await NeuerJob(stapelGroesse: 2).StapelVersendenAsync(TestContext.Current.CancellationToken);

        bearbeitet.ShouldBe(2);
        _transport.Gesendet.Select(m => m.To.Mailboxes.Single().Address).ShouldBe(["hoch@example.org", "normal1@example.org"]);
        (await AlleEintraegeAsync()).Single(m => m.An == "normal2@example.org").Status.ShouldBe(EmailStatus.Wartend);
    }

    [DatenbankFact]
    public async Task Sendefehler_verschiebt_nur_die_betroffene_Mail()
    {
        await EinreihenAsync(Mail("kaputt@example.org"), Mail("ok@example.org"));
        _transport.FehlerBeimSenden = m =>
            m.To.Mailboxes.Single().Address == "kaputt@example.org" ? new InvalidOperationException("Postfach voll") : null;

        await NeuerJob().StapelVersendenAsync(TestContext.Current.CancellationToken);

        var eintraege = await AlleEintraegeAsync();
        var kaputt = eintraege.Single(m => m.An == "kaputt@example.org");
        kaputt.Status.ShouldBe(EmailStatus.Wartend);
        kaputt.Versuche.ShouldBe(1);
        kaputt.LetzterFehler.ShouldBe("Postfach voll");
        kaputt.FaelligAbUtc.ShouldBe(Start.UtcDateTime.AddMinutes(1));
        eintraege.Single(m => m.An == "ok@example.org").Status.ShouldBe(EmailStatus.Gesendet);
    }

    [DatenbankFact]
    public async Task Verschobene_Mail_wird_erst_nach_der_Wartezeit_erneut_versucht_und_gibt_nach_MaxVersuche_auf()
    {
        await EinreihenAsync(Mail("kaputt@example.org"));
        _transport.FehlerBeimSenden = _ => new InvalidOperationException("abgelehnt");
        var job = NeuerJob(maxVersuche: 2);
        var abbruch = TestContext.Current.CancellationToken;

        (await job.StapelVersendenAsync(abbruch)).ShouldBe(1);
        (await job.StapelVersendenAsync(abbruch)).ShouldBe(0, "noch nicht wieder fällig");

        _zeit.Advance(TimeSpan.FromMinutes(1));
        (await job.StapelVersendenAsync(abbruch)).ShouldBe(1);

        var eintrag = (await AlleEintraegeAsync()).Single();
        eintrag.Versuche.ShouldBe(2);
        eintrag.Status.ShouldBe(EmailStatus.Fehlgeschlagen);

        _zeit.Advance(TimeSpan.FromDays(1));
        (await job.StapelVersendenAsync(abbruch)).ShouldBe(0, "endgültig fehlgeschlagene Mails werden nicht mehr versucht");
    }

    [DatenbankFact]
    public async Task Nicht_erreichbarer_SMTP_Server_veraendert_nichts()
    {
        await EinreihenAsync(Mail("a@example.org"));
        _transport.FehlerBeimVerbinden = new InvalidOperationException("SMTP nicht erreichbar");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            NeuerJob().StapelVersendenAsync(TestContext.Current.CancellationToken));

        var eintrag = (await AlleEintraegeAsync()).Single();
        eintrag.Status.ShouldBe(EmailStatus.Wartend);
        eintrag.Versuche.ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Alte_abgeschlossene_Eintraege_werden_geloescht_wartende_bleiben()
    {
        await EinreihenAsync(Mail("alt-gesendet@example.org"), Mail("alt-wartend@example.org"));
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var gesendet = await kontext.EmailAusgang.SingleAsync(m => m.An == "alt-gesendet@example.org", TestContext.Current.CancellationToken);
            gesendet.Status = EmailStatus.Gesendet;
            await kontext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        _zeit.Advance(TimeSpan.FromDays(31));
        await EinreihenAsync(Mail("neu@example.org"));
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var neu = await kontext.EmailAusgang.SingleAsync(m => m.An == "neu@example.org", TestContext.Current.CancellationToken);
            neu.Status = EmailStatus.Gesendet;
            await kontext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var geloescht = await NeuerJob().AlteEintraegeLoeschenAsync(TestContext.Current.CancellationToken);

        geloescht.ShouldBe(1);
        (await AlleEintraegeAsync()).Select(m => m.An).ShouldBe(["alt-wartend@example.org", "neu@example.org"]);
    }
}
