using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Services.Systemzustand;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

/// <summary>Die Prüfungen aus Services/Systemzustand gegen echte Daten: jeweils ein Befund und der Normalfall ohne Befund.</summary>
[Trait("Category", "Integration")]
public class SystemzustandChecksTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    // 02.10.2026 12:00 Ortszeit
    private static readonly DateTime Jetzt = new(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(Jetzt));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private TestKontextFabrik Fabrik => new(Datenbank);

    private static IOptions<VeranstaltungenOptionen> Modul(bool aktiviert = true) =>
        Options.Create(new VeranstaltungenOptionen { Aktiviert = aktiviert });

    private Task<HealthCheckResult> PruefenAsync(IHealthCheck check) => check.CheckHealthAsync(new HealthCheckContext(), Abbruch);

    private static DbImage Bild(string titel, DateTime erstellt, DateTime? vorlaeufigSeit = null) =>
        new() { Title = titel, ImageData = new byte[1024 * 1024], ContentType = "image/jpeg", CreatedAt = erstellt, VorlaeufigSeitUtc = vorlaeufigSeit };

    private async Task SpeichernAsync(params object[] eintraege)
    {
        await using var kontext = Datenbank.NeuerKontext();
        kontext.AddRange(eintraege);
        await kontext.SaveChangesAsync(Abbruch);
    }

    private static Veranstaltung Veranstaltung(string titel, VeranstaltungStatus status = VeranstaltungStatus.Veroeffentlicht,
        VeranstaltungSichtbarkeit sichtbarkeit = VeranstaltungSichtbarkeit.Oeffentlich, int? maxTeilnehmer = null) =>
        new()
        {
            Titel = titel,
            Slug = titel.ToLowerInvariant(),
            Status = status,
            Sichtbarkeit = sichtbarkeit,
            ErstelltUtc = Jetzt,
            Tage = { new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), MaxTeilnehmer = maxTeilnehmer } }
        };

    private static Anmeldung Anmeldung(string email, AnmeldungStatus status = AnmeldungStatus.Angemeldet, DateTime? reserviertBis = null) =>
        new()
        {
            Email = email,
            Vorname = "Max",
            Nachname = "Muster",
            TokenHash = AnmeldeToken.Erzeugen().Hash,
            Status = status,
            ReserviertBisUtc = reserviertBis,
            ErstelltUtc = Jetzt.AddDays(-20)
        };

    private static BenachrichtigungEmpfaenger Empfaenger(BenachrichtigungModus modus = BenachrichtigungModus.Sofort) =>
        new() { Email = "orga@example.org", Modus = modus, ErstelltUtc = Jetzt };

    [DatenbankFact]
    public async Task Datenbank_ist_erreichbar_und_migriert()
    {
        var ergebnis = await PruefenAsync(new DatenbankCheck(Fabrik));

        ergebnis.Status.ShouldBe(HealthStatus.Healthy);
    }

    [DatenbankFact]
    public async Task Bilder_meldet_lose_und_nicht_aufgeraeumte_aber_nicht_verwendete_oder_frische()
    {
        var lose = Bild("lose", Jetzt.AddDays(-3));
        await SpeichernAsync(
            lose,
            Bild("gerade hochgeladen", Jetzt.AddHours(-2)),
            Bild("vorlaeufig, Editor evtl. noch offen", Jetzt.AddDays(-2), vorlaeufigSeit: Jetzt - BildAufraeumJob.AufbewahrenFuer + TimeSpan.FromHours(1)),
            Bild("vorlaeufig, gerade fällig", Jetzt.AddDays(-3), vorlaeufigSeit: Jetzt - BildAufraeumJob.AufbewahrenFuer - TimeSpan.FromHours(1)),
            Bild("vorlaeufig, längst fällig", Jetzt.AddDays(-5), vorlaeufigSeit: Jetzt - BildAufraeumJob.AufbewahrenFuer - TimeSpan.FromHours(4)),
            new GalerieEintrag { DbImage = Bild("in der Galerie", Jetzt.AddDays(-30)) });

        var ergebnis = await PruefenAsync(new BilderCheck(Fabrik, _zeit));

        ergebnis.Status.ShouldBe(HealthStatus.Degraded);
        ergebnis.Data["lose"].ShouldBe(1);
        ergebnis.Data["loseIds"].ShouldBeOfType<List<int>>().ShouldBe([lose.Id]);
        ergebnis.Data["loseMegabyte"].ShouldBe(1.0);
        // Nur was über die Toleranz hinaus liegt: der gerade fällige Upload ist Sache des nächsten Job-Durchlaufs
        ergebnis.Data["vorlaeufigNichtAufgeraeumt"].ShouldBe(1);
        ergebnis.Description.ShouldNotBeNull().ShouldContain($"IDs: {lose.Id}.");
    }

    [DatenbankFact]
    public async Task Bilder_ohne_Befund_wenn_alles_verwendet_ist()
    {
        await SpeichernAsync(new GalerieEintrag { DbImage = Bild("in der Galerie", Jetzt.AddDays(-30)) });

        var ergebnis = await PruefenAsync(new BilderCheck(Fabrik, _zeit));

        ergebnis.Status.ShouldBe(HealthStatus.Healthy);
    }

    [DatenbankFact]
    public async Task Mail_Warteschlange_ueberfaellig_ist_ungesund_fehlgeschlagen_eingeschraenkt()
    {
        var check = new MailWarteschlangeCheck(Fabrik, _zeit);
        EmailAusgang Mail(EmailStatus status, DateTime faelligAb) =>
            new() { An = "x@example.org", Betreff = "B", Html = "H", Status = status, ErstelltUtc = faelligAb, FaelligAbUtc = faelligAb };

        await SpeichernAsync(Mail(EmailStatus.Wartend, Jetzt.AddMinutes(-5)));
        (await PruefenAsync(check)).Status.ShouldBe(HealthStatus.Healthy);

        await SpeichernAsync(Mail(EmailStatus.Fehlgeschlagen, Jetzt.AddDays(-1)), Mail(EmailStatus.Fehlgeschlagen, Jetzt.AddDays(-10)));
        var eingeschraenkt = await PruefenAsync(check);
        eingeschraenkt.Status.ShouldBe(HealthStatus.Degraded);
        eingeschraenkt.Data["fehlgeschlagen"].ShouldBe(1);

        await SpeichernAsync(Mail(EmailStatus.Wartend, Jetzt - MailWarteschlangeCheck.StauNach - TimeSpan.FromMinutes(1)));
        var ungesund = await PruefenAsync(check);
        ungesund.Status.ShouldBe(HealthStatus.Unhealthy);
        ungesund.Data["ueberfaellig"].ShouldBe(1);
        ungesund.Description.ShouldNotBeNull().ShouldNotContain("x@example.org");
    }

    [DatenbankFact]
    public async Task Wartung_meldet_liegengebliebene_Anmeldungen_und_Veranstaltungen()
    {
        var vorbei = Veranstaltung("Sommerfest");
        vorbei.Tage.Single().Datum = new DateOnly(2026, 9, 28);
        var kommend = Veranstaltung("Herbstseminar");
        kommend.Anmeldungen.Add(Anmeldung("alt@example.org", AnmeldungStatus.Unbestaetigt, Jetzt - VeranstaltungWartungJob.VerwerfenNach - TimeSpan.FromHours(4)));
        kommend.Anmeldungen.Add(Anmeldung("knapp@example.org", AnmeldungStatus.Unbestaetigt, Jetzt - VeranstaltungWartungJob.VerwerfenNach - TimeSpan.FromHours(1)));
        await SpeichernAsync(vorbei, kommend);

        var ergebnis = await PruefenAsync(new VeranstaltungWartungCheck(Fabrik, _zeit, Modul()));

        ergebnis.Status.ShouldBe(HealthStatus.Degraded);
        ergebnis.Description.ShouldNotBeNull().ShouldContain("1 unbestätigte Anmeldung(en)");
        ergebnis.Description.ShouldContain("Vorbei, aber nicht abgeschlossen: Sommerfest.");

        (await PruefenAsync(new VeranstaltungWartungCheck(Fabrik, _zeit, Modul(aktiviert: false)))).Status.ShouldBe(HealthStatus.Healthy);
    }

    [DatenbankFact]
    public async Task Kalender_meldet_fehlende_und_unerwartete_Eintraege()
    {
        var ohneEintrag = Veranstaltung("Ohne Eintrag");
        var richtig = Veranstaltung("Richtig");
        var nurPerLink = Veranstaltung("Nur per Link", sichtbarkeit: VeranstaltungSichtbarkeit.NurPerLink);
        var entwurf = Veranstaltung("Entwurf", status: VeranstaltungStatus.Entwurf);
        await SpeichernAsync(ohneEintrag, richtig, nurPerLink, entwurf);
        AppointmentData Eintrag(Veranstaltung v) => new()
        {
            Subject = v.Titel,
            StartTime = new DateTime(2026, 11, 14, 10, 0, 0),
            EndTime = new DateTime(2026, 11, 14, 12, 0, 0),
            VeranstaltungsTagId = v.Tage.Single().Id
        };
        await SpeichernAsync(Eintrag(richtig), Eintrag(nurPerLink));

        var ergebnis = await PruefenAsync(new VeranstaltungKalenderCheck(Fabrik, Modul()));

        ergebnis.Status.ShouldBe(HealthStatus.Degraded);
        var text = ergebnis.Description.ShouldNotBeNull();
        text.ShouldContain("Fehlt im Kalender: Ohne Eintrag (14.11.2026 10:00).");
        text.ShouldContain("nicht (oder mehrfach) vorgesehen: Nur per Link (14.11.2026 10:00).");
        text.ShouldNotContain("Richtig");
        text.ShouldNotContain("Entwurf");
    }

    [DatenbankFact]
    public async Task Anmeldungen_meldet_Ueberbuchung_und_fehlende_Empfaenger()
    {
        var ueberbucht = Veranstaltung("Voll", maxTeilnehmer: 1);
        ueberbucht.BenachrichtigungEmpfaenger.Add(Empfaenger());
        ueberbucht.Anmeldungen.Add(Anmeldung("a@example.org"));
        ueberbucht.Anmeldungen.Add(Anmeldung("b@example.org"));
        // Abgelaufene Reservierung und Abmeldung belegen keinen Platz
        var passt = Veranstaltung("Passt", maxTeilnehmer: 1);
        passt.BenachrichtigungEmpfaenger.Add(Empfaenger());
        passt.Anmeldungen.Add(Anmeldung("c@example.org"));
        passt.Anmeldungen.Add(Anmeldung("d@example.org", AnmeldungStatus.Unbestaetigt, reserviertBis: Jetzt.AddHours(-1)));
        passt.Anmeldungen.Add(Anmeldung("e@example.org", AnmeldungStatus.Storniert));
        var nurPausiert = Veranstaltung("Pausiert");
        nurPausiert.BenachrichtigungEmpfaenger.Add(Empfaenger(BenachrichtigungModus.Pausiert));
        var entwurf = Veranstaltung("Entwurf", status: VeranstaltungStatus.Entwurf);
        await SpeichernAsync(ueberbucht, passt, nurPausiert, entwurf);

        var ergebnis = await PruefenAsync(new VeranstaltungAnmeldungenCheck(Fabrik, _zeit, Modul()));

        ergebnis.Status.ShouldBe(HealthStatus.Degraded);
        var text = ergebnis.Description.ShouldNotBeNull();
        text.ShouldContain("Überbucht: Voll (14.11.2026 10:00: 2 von 1).");
        text.ShouldContain("Niemand wird über Anmeldungen benachrichtigt: Pausiert.");
        text.ShouldNotContain("Passt");
        text.ShouldNotContain("Entwurf");
    }
}
