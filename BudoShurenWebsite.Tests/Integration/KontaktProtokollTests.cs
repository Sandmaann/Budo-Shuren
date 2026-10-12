using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Kontakt;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

[Trait("Category", "Integration")]
public class KontaktProtokollTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTime Jetzt = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(Jetzt));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private KontaktProtokoll Protokoll => new(new TestKontextFabrik(Datenbank), _zeit, NullLogger<KontaktProtokoll>.Instance);

    private static KontaktAnfrage Anfrage(string abteilung, KontaktAnfrageStatus status = KontaktAnfrageStatus.Zugestellt, int mails = 0) =>
        new()
        {
            Abteilung = abteilung,
            AbsenderEmail = "max@example.org",
            Nachricht = "Hallo",
            Status = status,
            AnzahlMails = mails,
            Seite = "Kontakt",
            Interaktiv = true
        };

    private async Task<List<KontaktAnfrage>> AlleAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.KontaktAnfragen.AsNoTracking().OrderBy(a => a.Id).ToListAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Eintrag_bekommt_den_Zeitpunkt_und_zu_lange_Texte_werden_gekuerzt()
    {
        var anfrage = Anfrage("Aikido", mails: 2);
        anfrage.Nachricht = new string('x', KontaktAnfrage.MaxNachricht + 500);
        anfrage.Hinweis = new string('h', 600);

        await Protokoll.EintragenAsync(anfrage);

        var gespeichert = (await AlleAsync()).ShouldHaveSingleItem();
        gespeichert.ZeitpunktUtc.ShouldBe(Jetzt);
        gespeichert.Abteilung.ShouldBe("Aikido");
        gespeichert.AbsenderEmail.ShouldBe("max@example.org");
        gespeichert.Nachricht!.Length.ShouldBe(KontaktAnfrage.MaxNachricht);
        gespeichert.Hinweis!.Length.ShouldBe(500);
        gespeichert.Status.ShouldBe(KontaktAnfrageStatus.Zugestellt);
        gespeichert.AnzahlMails.ShouldBe(2);
        gespeichert.Seite.ShouldBe("Kontakt");
        gespeichert.Interaktiv.ShouldBeTrue();
    }

    [DatenbankFact]
    public async Task Auswertung_zaehlt_je_Abteilung_und_Ablehnungen_je_Grund_nur_im_Zeitraum()
    {
        await Protokoll.EintragenAsync(Anfrage("Aikido", mails: 5)); // vor dem Zeitraum
        _zeit.Advance(TimeSpan.FromDays(10));
        var ab = _zeit.GetUtcNow().UtcDateTime;

        await Protokoll.EintragenAsync(Anfrage("Aikido", mails: 2));
        await Protokoll.EintragenAsync(Anfrage("Aikido", mails: 1));
        await Protokoll.EintragenAsync(Anfrage("Aikido", KontaktAnfrageStatus.VersandFehler));
        await Protokoll.EintragenAsync(Anfrage("Aikido", KontaktAnfrageStatus.Honeypot));
        await Protokoll.EintragenAsync(Anfrage("Bujinkan", KontaktAnfrageStatus.Honeypot));
        await Protokoll.EintragenAsync(Anfrage("Bujinkan", KontaktAnfrageStatus.Inhalt));
        await Protokoll.EintragenAsync(Anfrage("Bujinkan", mails: 3));

        var auswertung = await Protokoll.AuswertenAsync(ab, Abbruch);

        auswertung.Abteilungen.ShouldBe([
            new KontaktAbteilungZahlen("Aikido", Zugestellt: 2, Mails: 3, VersandFehler: 1, Abgelehnt: 1),
            new KontaktAbteilungZahlen("Bujinkan", Zugestellt: 1, Mails: 3, VersandFehler: 0, Abgelehnt: 2)
        ]);
        auswertung.Ablehnungen.ShouldBe([
            new KontaktAblehnungZahl(KontaktAnfrageStatus.Honeypot, 2),
            new KontaktAblehnungZahl(KontaktAnfrageStatus.Inhalt, 1)
        ]);

        (await Protokoll.AuswertenAsync(ab.AddDays(1), Abbruch)).Abteilungen.ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Verlauf_zeigt_die_neuesten_zuerst_und_abgelehnte_nur_auf_Wunsch()
    {
        await Protokoll.EintragenAsync(Anfrage("Aikido", mails: 1));
        _zeit.Advance(TimeSpan.FromMinutes(1));
        await Protokoll.EintragenAsync(Anfrage("Bujinkan", KontaktAnfrageStatus.ZuSchnell));
        _zeit.Advance(TimeSpan.FromMinutes(1));
        await Protokoll.EintragenAsync(Anfrage("Genbukan", KontaktAnfrageStatus.VersandFehler));

        (await Protokoll.LetzteAsync(10, mitAbgelehnten: true, Abbruch)).Select(a => a.Abteilung).ShouldBe(["Genbukan", "Bujinkan", "Aikido"]);
        (await Protokoll.LetzteAsync(10, mitAbgelehnten: false, Abbruch)).Select(a => a.Abteilung).ShouldBe(["Genbukan", "Aikido"]);
        (await Protokoll.LetzteAsync(1, mitAbgelehnten: true, Abbruch)).Select(a => a.Abteilung).ShouldBe(["Genbukan"]);
    }
}
