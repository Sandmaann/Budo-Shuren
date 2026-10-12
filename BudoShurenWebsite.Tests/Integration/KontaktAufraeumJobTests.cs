using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Kontakt;
using BudoShurenWebsite.Services.Systemzustand;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

/// <summary>Aufbewahrungsfrist im Protokoll des Kontaktformulars: der Job leert, die Prüfung meldet Liegengebliebenes.</summary>
[Trait("Category", "Integration")]
public class KontaktAufraeumJobTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTime Jetzt = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(Jetzt));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private KontaktAufraeumJob Job => new(new TestKontextFabrik(Datenbank), _zeit);

    private static KontaktAnfrage Anfrage(string abteilung, DateTime zeitpunkt, KontaktAnfrageStatus status = KontaktAnfrageStatus.Zugestellt) =>
        new()
        {
            ZeitpunktUtc = zeitpunkt,
            Abteilung = abteilung,
            AbsenderEmail = "max@example.org",
            Nachricht = "Hallo",
            Status = status,
            Hinweis = "Hinweis",
            AnzahlMails = 2,
            Seite = "Kontakt",
            Interaktiv = true
        };

    private async Task SpeichernAsync(params KontaktAnfrage[] anfragen)
    {
        await using var kontext = Datenbank.NeuerKontext();
        kontext.KontaktAnfragen.AddRange(anfragen);
        await kontext.SaveChangesAsync(Abbruch);
    }

    private async Task<List<KontaktAnfrage>> AlleAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.KontaktAnfragen.AsNoTracking().OrderBy(a => a.Abteilung).ToListAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Leert_Absender_und_Nachricht_nur_nach_der_Frist_und_laesst_den_Rest_der_Zeile()
    {
        await SpeichernAsync(
            Anfrage("A abgelaufen", Jetzt - KontaktAufraeumJob.AufbewahrenFuer - TimeSpan.FromMinutes(1)),
            Anfrage("B abgelaufen, Spam", Jetzt - KontaktAufraeumJob.AufbewahrenFuer - TimeSpan.FromDays(30), KontaktAnfrageStatus.Honeypot),
            Anfrage("C noch in der Frist", Jetzt - KontaktAufraeumJob.AufbewahrenFuer + TimeSpan.FromMinutes(1)),
            Anfrage("D neu", Jetzt));

        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(2);

        var alle = await AlleAsync();
        alle.Select(a => a.AbsenderEmail).ShouldBe([null, null, "max@example.org", "max@example.org"]);
        alle.Select(a => a.Nachricht).ShouldBe([null, null, "Hallo", "Hallo"]);
        // Für die Zahlen im Adminbereich bleibt alles andere stehen
        var geleert = alle[0];
        geleert.Abteilung.ShouldBe("A abgelaufen");
        geleert.ZeitpunktUtc.ShouldBe(Jetzt - KontaktAufraeumJob.AufbewahrenFuer - TimeSpan.FromMinutes(1));
        geleert.Status.ShouldBe(KontaktAnfrageStatus.Zugestellt);
        geleert.AnzahlMails.ShouldBe(2);
        geleert.Seite.ShouldBe("Kontakt");

        (await Job.AusfuehrenAsync(Abbruch)).ShouldBe(0, "schon geleerte Einträge fasst der Job nicht noch einmal an");
    }

    [DatenbankFact]
    public async Task Pruefung_meldet_nur_was_der_Job_laengst_haette_leeren_muessen()
    {
        var check = new KontaktProtokollCheck(new TestKontextFabrik(Datenbank), _zeit);
        Task<HealthCheckResult> PruefenAsync() => check.CheckHealthAsync(new HealthCheckContext(), Abbruch);

        // Gerade fällig: Sache des nächsten Durchlaufs, noch kein Befund
        await SpeichernAsync(Anfrage("gerade fällig", Jetzt - KontaktAufraeumJob.AufbewahrenFuer - TimeSpan.FromHours(1)));
        (await PruefenAsync()).Status.ShouldBe(HealthStatus.Healthy);

        await SpeichernAsync(Anfrage("längst fällig", Jetzt - KontaktAufraeumJob.AufbewahrenFuer - TimeSpan.FromHours(4)));
        var befund = await PruefenAsync();
        befund.Status.ShouldBe(HealthStatus.Degraded);
        befund.Data["nichtGeleert"].ShouldBe(1);
        befund.Description.ShouldNotBeNull().ShouldContain("Bei 1 Nachricht(en)");

        await Job.AusfuehrenAsync(Abbruch);
        (await PruefenAsync()).Status.ShouldBe(HealthStatus.Healthy);
    }
}
