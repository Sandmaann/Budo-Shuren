using System.Security.Cryptography;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Tests.Integration.Veranstaltungen;

/// <summary>Regeln, die die Datenbank selbst durchsetzt (Indizes, Constraints, Löschverhalten, Concurrency).</summary>
[Trait("Category", "Integration")]
public class VeranstaltungSchemaTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private static readonly DateTime Jetzt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private static Veranstaltung NeueVeranstaltung(string slug = "herbstseminar") => new()
    {
        Titel = "Herbstseminar",
        Slug = slug,
        Teilnahmemodus = Teilnahmemodus.EinzelneTage,
        ErstelltUtc = Jetzt,
        Tage =
        {
            new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 30), MaxTeilnehmer = 20 },
            new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0) }
        }
    };

    private static Anmeldung NeueAnmeldung(Veranstaltung veranstaltung, string email = "max@example.org") => new()
    {
        Veranstaltung = veranstaltung,
        Email = email,
        Vorname = "Max",
        Nachname = "Muster",
        TokenHash = RandomNumberGenerator.GetBytes(32),
        TokenErstelltUtc = Jetzt,
        DatenschutzAkzeptiertUtc = Jetzt,
        ErstelltUtc = Jetzt
    };

    private async Task<int> SpeichernAsync(params object[] entities)
    {
        await using var kontext = Datenbank.NeuerKontext();
        kontext.AddRange(entities);
        await kontext.SaveChangesAsync(Abbruch);
        return entities.OfType<Veranstaltung>().FirstOrDefault()?.Id ?? 0;
    }

    [DatenbankFact]
    public async Task Veranstaltung_mit_Anmeldung_wird_vollstaendig_gespeichert_und_geladen()
    {
        var veranstaltung = NeueVeranstaltung();
        var anmeldung = NeueAnmeldung(veranstaltung);
        anmeldung.AnzahlBegleitpersonen = 2;
        anmeldung.Tage.Add(new AnmeldungTag { VeranstaltungsTag = veranstaltung.Tage.First() });
        anmeldung.InfoEmails.Add(new AnmeldungInfoEmail { Email = "begleitung@example.org", AbmeldeTokenHash = RandomNumberGenerator.GetBytes(32) });
        anmeldung.Ereignisse.Add(new AnmeldungEreignis { ZeitpunktUtc = Jetzt, Akteur = EreignisAkteur.Teilnehmer, Art = AnmeldungEreignisArt.Angelegt });
        var id = await SpeichernAsync(veranstaltung, anmeldung);

        await using var kontext = Datenbank.NeuerKontext();
        var geladen = await kontext.Veranstaltungen
            .Include(v => v.Tage)
            .Include(v => v.Anmeldungen).ThenInclude(a => a.Tage)
            .Include(v => v.Anmeldungen).ThenInclude(a => a.InfoEmails)
            .Include(v => v.Anmeldungen).ThenInclude(a => a.Ereignisse)
            .SingleAsync(v => v.Id == id, Abbruch);

        geladen.Tage.OrderBy(t => t.Datum).Select(t => (t.Datum, t.Beginn, t.Ende, t.MaxTeilnehmer)).ShouldBe(
        [
            (new DateOnly(2026, 11, 14), new TimeOnly(10, 0), new TimeOnly(16, 30), (int?)20),
            (new DateOnly(2026, 11, 15), new TimeOnly(9, 0), new TimeOnly(13, 0), (int?)null)
        ]);
        geladen.ZusammenfassungUhrzeit.ShouldBe(new TimeOnly(7, 0));
        geladen.RowVersion.ShouldNotBeEmpty();
        var a = geladen.Anmeldungen.ShouldHaveSingleItem();
        a.TokenHash.ShouldBe(anmeldung.TokenHash);
        a.Tage.ShouldHaveSingleItem().VeranstaltungsTagId.ShouldBe(geladen.Tage.Single(t => t.Datum.Day == 14).Id);
        a.InfoEmails.ShouldHaveSingleItem().Email.ShouldBe("begleitung@example.org");
        a.Ereignisse.ShouldHaveSingleItem().Art.ShouldBe(AnmeldungEreignisArt.Angelegt);
    }

    [DatenbankFact]
    public async Task Slug_ist_eindeutig()
    {
        await SpeichernAsync(NeueVeranstaltung("seminar"));

        await Should.ThrowAsync<DbUpdateException>(() => SpeichernAsync(NeueVeranstaltung("seminar")));
    }

    [DatenbankFact]
    public async Task Pro_Veranstaltung_nur_eine_Anmeldung_je_Adresse()
    {
        var erste = NeueVeranstaltung("erste");
        var zweite = NeueVeranstaltung("zweite");
        await SpeichernAsync(erste, zweite, NeueAnmeldung(erste), NeueAnmeldung(zweite)); // andere Veranstaltung: erlaubt

        await using var kontext = Datenbank.NeuerKontext();
        var vorhandene = await kontext.Veranstaltungen.SingleAsync(v => v.Slug == "erste", Abbruch);
        kontext.Add(NeueAnmeldung(vorhandene));
        await Should.ThrowAsync<DbUpdateException>(() => kontext.SaveChangesAsync(Abbruch));
    }

    [DatenbankFact]
    public async Task Zwei_Tage_am_selben_Datum_sind_nicht_erlaubt()
    {
        var veranstaltung = NeueVeranstaltung();
        veranstaltung.Tage.Add(new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(18, 0), Ende = new TimeOnly(20, 0) });

        await Should.ThrowAsync<DbUpdateException>(() => SpeichernAsync(veranstaltung));
    }

    [DatenbankFact]
    public async Task Empfaenger_braucht_genau_Benutzer_oder_Adresse()
    {
        var user = new ApplicationUser { UserName = "organisator", Email = "orga@example.org" };
        var veranstaltung = NeueVeranstaltung();
        veranstaltung.BenachrichtigungEmpfaenger.Add(new BenachrichtigungEmpfaenger { User = user, ErstelltUtc = Jetzt, BenachrichtigtBisUtc = Jetzt });
        veranstaltung.BenachrichtigungEmpfaenger.Add(new BenachrichtigungEmpfaenger { Email = "kasse@example.org", AbmeldeTokenHash = RandomNumberGenerator.GetBytes(32), ErstelltUtc = Jetzt, BenachrichtigtBisUtc = Jetzt });
        var id = await SpeichernAsync(user, veranstaltung);

        await Should.ThrowAsync<DbUpdateException>(() => EmpfaengerHinzufuegenAsync(id, new BenachrichtigungEmpfaenger()));
        await Should.ThrowAsync<DbUpdateException>(() => EmpfaengerHinzufuegenAsync(id, new BenachrichtigungEmpfaenger { UserId = user.Id, Email = "beides@example.org" }));
        await Should.ThrowAsync<DbUpdateException>(() => EmpfaengerHinzufuegenAsync(id, new BenachrichtigungEmpfaenger { UserId = user.Id }), "derselbe Benutzer zweimal");
    }

    private async Task EmpfaengerHinzufuegenAsync(int veranstaltungId, BenachrichtigungEmpfaenger empfaenger)
    {
        await using var kontext = Datenbank.NeuerKontext();
        empfaenger.VeranstaltungId = veranstaltungId;
        empfaenger.ErstelltUtc = Jetzt;
        empfaenger.BenachrichtigtBisUtc = Jetzt;
        kontext.Add(empfaenger);
        await kontext.SaveChangesAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Gebuchter_Tag_kann_nicht_geloescht_werden()
    {
        var veranstaltung = NeueVeranstaltung();
        var anmeldung = NeueAnmeldung(veranstaltung);
        anmeldung.Tage.Add(new AnmeldungTag { VeranstaltungsTag = veranstaltung.Tage.First() });
        await SpeichernAsync(veranstaltung, anmeldung);

        await using var kontext = Datenbank.NeuerKontext();
        var tag = await kontext.VeranstaltungsTage.SingleAsync(t => t.Datum == new DateOnly(2026, 11, 14), Abbruch);
        kontext.VeranstaltungsTage.Remove(tag);

        await Should.ThrowAsync<DbUpdateException>(() => kontext.SaveChangesAsync(Abbruch));
    }

    [DatenbankFact]
    public async Task Loeschen_eines_Entwurfs_entfernt_Tage_Anmeldungen_und_Empfaenger()
    {
        var veranstaltung = NeueVeranstaltung();
        var anmeldung = NeueAnmeldung(veranstaltung);
        anmeldung.InfoEmails.Add(new AnmeldungInfoEmail { Email = "begleitung@example.org", AbmeldeTokenHash = RandomNumberGenerator.GetBytes(32) });
        anmeldung.Ereignisse.Add(new AnmeldungEreignis { ZeitpunktUtc = Jetzt, Art = AnmeldungEreignisArt.Angelegt });
        veranstaltung.BenachrichtigungEmpfaenger.Add(new BenachrichtigungEmpfaenger { Email = "kasse@example.org", AbmeldeTokenHash = RandomNumberGenerator.GetBytes(32), ErstelltUtc = Jetzt, BenachrichtigtBisUtc = Jetzt });
        var id = await SpeichernAsync(veranstaltung, anmeldung);

        await using var kontext = Datenbank.NeuerKontext();
        await kontext.Veranstaltungen.Where(v => v.Id == id).ExecuteDeleteAsync(Abbruch);

        (await kontext.VeranstaltungsTage.CountAsync(Abbruch)).ShouldBe(0);
        (await kontext.Anmeldungen.CountAsync(Abbruch)).ShouldBe(0);
        (await kontext.AnmeldungInfoEmails.CountAsync(Abbruch)).ShouldBe(0);
        (await kontext.AnmeldungEreignisse.CountAsync(Abbruch)).ShouldBe(0);
        (await kontext.BenachrichtigungEmpfaenger.CountAsync(Abbruch)).ShouldBe(0);
    }

    [DatenbankFact]
    public async Task Gleichzeitige_Aenderung_einer_Anmeldung_wird_erkannt()
    {
        var veranstaltung = NeueVeranstaltung();
        await SpeichernAsync(veranstaltung, NeueAnmeldung(veranstaltung));

        await using var teilnehmer = Datenbank.NeuerKontext();
        await using var organisator = Datenbank.NeuerKontext();
        var ausSicht1 = await teilnehmer.Anmeldungen.SingleAsync(Abbruch);
        var ausSicht2 = await organisator.Anmeldungen.SingleAsync(Abbruch);

        ausSicht1.AnzahlBegleitpersonen = 1;
        await teilnehmer.SaveChangesAsync(Abbruch);

        ausSicht2.AdminNotiz = "Hat angerufen";
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => organisator.SaveChangesAsync(Abbruch));
    }
}
