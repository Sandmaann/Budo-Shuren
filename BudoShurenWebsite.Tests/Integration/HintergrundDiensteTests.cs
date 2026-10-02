using BudoShurenWebsite.Data;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Services.Mail;
using BudoShurenWebsite.Services.Systemzustand;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

/// <summary>
/// Die Hüllen der Hintergrunddienste wirklich gestartet (in der Test-App sind sie abgeschaltet):
/// erster Durchlauf, Lebenszeichen im DienstHerzschlag, Fehler beenden den Dienst nicht.
/// </summary>
[Trait("Category", "Integration")]
public class HintergrundDiensteTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
    private DienstHerzschlag? _herzschlag;

    // Feldinitialisierer dürfen _zeit nicht verwenden, deshalb beim ersten Zugriff
    private DienstHerzschlag Herzschlag => _herzschlag ??= new DienstHerzschlag(_zeit);

    /// <summary>Datenbank nicht erreichbar.</summary>
    private sealed class KaputteFabrik : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => throw new InvalidOperationException("Datenbank nicht erreichbar");
    }

    private static IConfiguration Konfiguration(bool bildAufraeumen = true) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BildAufraeumen:Aktiviert"] = bildAufraeumen.ToString() }).Build();

    /// <summary>Startet den Dienst, wartet auf den ersten gemeldeten Durchlauf und hält ihn wieder an.</summary>
    private async Task<DienstStand> ErsterDurchlaufAsync(BackgroundService dienst, string name)
    {
        var abbruch = TestContext.Current.CancellationToken;
        await dienst.StartAsync(abbruch);
        try
        {
            var bis = DateTime.UtcNow.AddSeconds(15);
            while (Herzschlag.Stand(name)?.LetzterLaufUtc is null && DateTime.UtcNow < bis)
                await Task.Delay(50, abbruch);
            return Herzschlag.Stand(name).ShouldNotBeNull();
        }
        finally
        {
            await dienst.StopAsync(abbruch);
        }
    }

    [DatenbankFact]
    public async Task Bilder_aufraeumen_meldet_Durchlauf()
    {
        var dienst = new BildAufraeumHostedService(new BildAufraeumJob(new TestKontextFabrik(Datenbank), _zeit), _zeit, Konfiguration(), Herzschlag, NullLogger<BildAufraeumHostedService>.Instance);

        var stand = await ErsterDurchlaufAsync(dienst, DienstHerzschlag.BildAufraeumen);

        stand.LetzterLaufUtc.ShouldNotBeNull();
        stand.LetzterLaufOk.ShouldBeTrue();
        DienstHerzschlag.Bewerten(stand, _zeit.GetUtcNow()).Status.ShouldBe(HealthStatus.Healthy);
    }

    [DatenbankFact]
    public async Task Fehler_im_Durchlauf_wird_als_eingeschraenkt_gemeldet()
    {
        var dienst = new BildAufraeumHostedService(new BildAufraeumJob(new KaputteFabrik(), _zeit), _zeit, Konfiguration(), Herzschlag, NullLogger<BildAufraeumHostedService>.Instance);

        var stand = await ErsterDurchlaufAsync(dienst, DienstHerzschlag.BildAufraeumen);

        stand.LetzterLaufOk.ShouldBeFalse();
        DienstHerzschlag.Bewerten(stand, _zeit.GetUtcNow()).Status.ShouldBe(HealthStatus.Degraded);
    }

    [DatenbankFact]
    public async Task Abgeschalteter_Dienst_meldet_sich_als_abgeschaltet()
    {
        var dienst = new BildAufraeumHostedService(new BildAufraeumJob(new TestKontextFabrik(Datenbank), _zeit), _zeit, Konfiguration(bildAufraeumen: false), Herzschlag, NullLogger<BildAufraeumHostedService>.Instance);

        await dienst.StartAsync(TestContext.Current.CancellationToken);
        await (dienst.ExecuteTask ?? Task.CompletedTask);

        var stand = Herzschlag.Stand(DienstHerzschlag.BildAufraeumen).ShouldNotBeNull();
        stand.Abgeschaltet.ShouldBe("BildAufraeumen:Aktiviert=false");
        DienstHerzschlag.Bewerten(stand, _zeit.GetUtcNow()).Status.ShouldBe(HealthStatus.Degraded);
    }

    [DatenbankFact]
    public async Task Mailversand_meldet_Durchlauf()
    {
        var optionen = Options.Create(new EmailVersandOptionen { PauseZwischenMails = TimeSpan.Zero });
        var job = new EmailVersandJob(new TestKontextFabrik(Datenbank), new FakeMailTransport(), _zeit, optionen, NullLogger<EmailVersandJob>.Instance);
        var dienst = new EmailVersandHostedService(job, new EmailVersandSignal(), _zeit, optionen, Herzschlag, NullLogger<EmailVersandHostedService>.Instance);

        var stand = await ErsterDurchlaufAsync(dienst, DienstHerzschlag.EmailVersand);

        stand.LetzterLaufOk.ShouldBeTrue();
    }

    [DatenbankFact]
    public async Task Veranstaltungs_Jobs_melden_Durchlauf()
    {
        var optionen = Options.Create(new VeranstaltungenOptionen { Aktiviert = true, HintergrundJobsAktiviert = true });
        var fabrik = new TestKontextFabrik(Datenbank);
        var benachrichtigung = new BenachrichtigungJob(fabrik, new EmailWarteschlange(_zeit, new EmailVersandSignal()), _zeit, optionen, NullLogger<BenachrichtigungJob>.Instance);
        var dienst = new VeranstaltungJobsHostedService(benachrichtigung, new VeranstaltungWartungJob(fabrik, _zeit), _zeit, optionen, Herzschlag, NullLogger<VeranstaltungJobsHostedService>.Instance);

        var stand = await ErsterDurchlaufAsync(dienst, DienstHerzschlag.Veranstaltungen);

        stand.LetzterLaufOk.ShouldBeTrue();
    }
}
