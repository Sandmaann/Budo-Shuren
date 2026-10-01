using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration.Veranstaltungen;

[Trait("Category", "Integration")]
public class VeranstaltungWartungJobTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    // 01.10.2026 12:00 Ortszeit
    private static readonly DateTime Jetzt = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(Jetzt));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private VeranstaltungWartungJob Job => new(new TestKontextFabrik(Datenbank), _zeit);

    private async Task<Veranstaltung> VeranstaltungAsync(string slug, VeranstaltungStatus status, params DateOnly[] tage)
    {
        await using var kontext = Datenbank.NeuerKontext();
        var v = new Veranstaltung { Titel = slug, Slug = slug, Status = status, ErstelltUtc = Jetzt };
        foreach (var datum in tage)
            v.Tage.Add(new VeranstaltungsTag { Datum = datum, Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0) });
        kontext.Veranstaltungen.Add(v);
        await kontext.SaveChangesAsync(Abbruch);
        return v;
    }

    private async Task<int> AnmeldungAsync(int veranstaltungId, string email, AnmeldungStatus status, DateTime? reserviertBis, DateTime? bestaetigt = null)
    {
        await using var kontext = Datenbank.NeuerKontext();
        var a = new Anmeldung
        {
            VeranstaltungId = veranstaltungId,
            Email = email,
            Vorname = "Max",
            Nachname = "Muster",
            TokenHash = AnmeldeToken.Erzeugen().Hash,
            Status = status,
            ReserviertBisUtc = reserviertBis,
            EmailBestaetigtUtc = bestaetigt,
            ErstelltUtc = Jetzt.AddDays(-20),
            Ereignisse = { new AnmeldungEreignis { ZeitpunktUtc = Jetzt.AddDays(-20), Art = AnmeldungEreignisArt.Angelegt } }
        };
        kontext.Anmeldungen.Add(a);
        await kontext.SaveChangesAsync(Abbruch);
        return a.Id;
    }

    [DatenbankFact]
    public async Task Verwirft_lange_abgelaufene_unbestaetigte_Anmeldungen()
    {
        var v = await VeranstaltungAsync("seminar", VeranstaltungStatus.Veroeffentlicht, new DateOnly(2026, 11, 14));
        var alt = await AnmeldungAsync(v.Id, "alt@example.org", AnmeldungStatus.Unbestaetigt, Jetzt.AddDays(-8));
        var frisch = await AnmeldungAsync(v.Id, "frisch@example.org", AnmeldungStatus.Unbestaetigt, Jetzt.AddDays(-1));
        var frueherBestaetigt = await AnmeldungAsync(v.Id, "wieder@example.org", AnmeldungStatus.Unbestaetigt, Jetzt.AddDays(-8), bestaetigt: Jetzt.AddDays(-30));
        var angemeldet = await AnmeldungAsync(v.Id, "da@example.org", AnmeldungStatus.Angemeldet, null, bestaetigt: Jetzt.AddDays(-30));

        (await Job.AusfuehrenAsync(Abbruch)).VerworfeneAnmeldungen.ShouldBe(2);

        await using var kontext = Datenbank.NeuerKontext();
        var uebrig = await kontext.Anmeldungen.Include(a => a.Ereignisse).ToDictionaryAsync(a => a.Id, Abbruch);
        uebrig.Keys.ShouldBe([frisch, frueherBestaetigt, angemeldet], ignoreOrder: true);
        (await kontext.AnmeldungEreignisse.AnyAsync(e => e.AnmeldungId == alt, Abbruch)).ShouldBeFalse("Historie wird mit gelöscht");

        var wieder = uebrig[frueherBestaetigt];
        wieder.Status.ShouldBe(AnmeldungStatus.Storniert);
        wieder.ReserviertBisUtc.ShouldBeNull();
        wieder.Ereignisse.ShouldContain(e => e.Art == AnmeldungEreignisArt.Storniert && e.Akteur == EreignisAkteur.System);
        uebrig[frisch].Status.ShouldBe(AnmeldungStatus.Unbestaetigt);
    }

    [DatenbankFact]
    public async Task Schliesst_vergangene_veroeffentlichte_Veranstaltungen_ab()
    {
        var vorbei = await VeranstaltungAsync("vorbei", VeranstaltungStatus.Veroeffentlicht, new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 30));
        var heute = await VeranstaltungAsync("heute", VeranstaltungStatus.Veroeffentlicht, new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1));
        var entwurf = await VeranstaltungAsync("entwurf", VeranstaltungStatus.Entwurf, new DateOnly(2026, 9, 1));
        var ohneTage = await VeranstaltungAsync("ohne-tage", VeranstaltungStatus.Veroeffentlicht);

        (await Job.AusfuehrenAsync(Abbruch)).AbgeschlosseneVeranstaltungen.ShouldBe(1);

        await using var kontext = Datenbank.NeuerKontext();
        var status = await kontext.Veranstaltungen.ToDictionaryAsync(v => v.Id, v => v.Status, Abbruch);
        status[vorbei.Id].ShouldBe(VeranstaltungStatus.Abgeschlossen);
        status[heute.Id].ShouldBe(VeranstaltungStatus.Veroeffentlicht);
        status[entwurf.Id].ShouldBe(VeranstaltungStatus.Entwurf);
        status[ohneTage.Id].ShouldBe(VeranstaltungStatus.Veroeffentlicht);
    }
}
