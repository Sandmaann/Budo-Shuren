using System.Text.RegularExpressions;
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
public class AnmeldungServiceTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Basis = "https://test.example/";
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _zeit = new(Start);

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private AnmeldungService Service => new(
        new TestKontextFabrik(Datenbank),
        new EmailWarteschlange(_zeit, new EmailVersandSignal()),
        _zeit,
        Options.Create(new VeranstaltungenOptionen { ReservierungStunden = 24, MaxInfoEmails = 5 }));

    private async Task<Veranstaltung> VeranstaltungAnlegenAsync(
        int? plaetze = 20, bool doubleOptIn = true, Teilnahmemodus modus = Teilnahmemodus.NurGesamt,
        VeranstaltungStatus status = VeranstaltungStatus.Veroeffentlicht, DateTime? anmeldungBis = null)
    {
        var v = new Veranstaltung
        {
            Titel = "Herbstseminar",
            Slug = "herbstseminar",
            Status = status,
            KontaktEmail = "seminar@example.org",
            DoubleOptIn = doubleOptIn,
            Teilnahmemodus = modus,
            MaxBegleitpersonen = 3,
            AnmeldungBis = anmeldungBis,
            ErstelltUtc = Start.UtcDateTime,
            Tage =
            {
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = plaetze },
                new VeranstaltungsTag { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0), MaxTeilnehmer = plaetze }
            }
        };
        await using var kontext = Datenbank.NeuerKontext();
        kontext.Veranstaltungen.Add(v);
        await kontext.SaveChangesAsync(Abbruch);
        return v;
    }

    private static AnmeldeEingabe Eingabe(string email = "max@example.org", int begleitung = 0, string? infoEmails = null, string vorname = "Max") => new()
    {
        Vorname = vorname,
        Nachname = "Muster",
        Email = email,
        AnzahlBegleitpersonen = begleitung,
        InfoEmails = infoEmails,
        DatenschutzAkzeptiert = true
    };

    private async Task<Anmeldung> AnmeldungLadenAsync(string email = "max@example.org")
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Anmeldungen.AsNoTracking()
            .Include(a => a.Ereignisse).Include(a => a.InfoEmails).Include(a => a.Tage)
            .SingleAsync(a => a.Email == email, Abbruch);
    }

    private async Task<List<EmailAusgang>> MailsAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.EmailAusgang.AsNoTracking().OrderBy(m => m.Id).ToListAsync(Abbruch);
    }

    private static string TokenAus(EmailAusgang mail, string pfad)
    {
        var treffer = Regex.Match(mail.Html, $"{Regex.Escape(Basis + pfad)}/([A-Za-z0-9_-]{{43}})");
        treffer.Success.ShouldBeTrue($"Link {pfad}/<token> fehlt in der Mail \"{mail.Betreff}\"");
        return treffer.Groups[1].Value;
    }

    private async Task<AnmeldeErgebnisArt> AnmeldenAsync(AnmeldeEingabe eingabe) =>
        (await Service.AnmeldenAsync("herbstseminar", eingabe, Basis, Abbruch)).Art;

    [DatenbankFact]
    public async Task Neue_Anmeldung_ist_unbestaetigt_reserviert_und_bekommt_den_Bestaetigungslink()
    {
        await VeranstaltungAnlegenAsync();

        (await AnmeldenAsync(Eingabe())).ShouldBe(AnmeldeErgebnisArt.EmailVersendet);

        var a = await AnmeldungLadenAsync();
        a.Status.ShouldBe(AnmeldungStatus.Unbestaetigt);
        a.ReserviertBisUtc.ShouldBe(Start.UtcDateTime.AddHours(24));
        a.Ereignisse.ShouldHaveSingleItem().Art.ShouldBe(AnmeldungEreignisArt.Angelegt);

        var mail = (await MailsAsync()).ShouldHaveSingleItem();
        mail.An.ShouldBe("max@example.org");
        mail.AntwortAn.ShouldBe("seminar@example.org");
        mail.Prioritaet.ShouldBe(EmailPrioritaet.Hoch);
        mail.BezugTyp.ShouldBe(AnmeldungService.BezugTyp);
        mail.BezugId.ShouldBe(a.Id);
        AnmeldeToken.Hash(TokenAus(mail, VeranstaltungLinks.Bestaetigen)).ShouldBe(a.TokenHash, "in der Datenbank steht nur der Hash");
    }

    [DatenbankFact]
    public async Task Bestaetigen_meldet_an_tauscht_den_Link_und_informiert_die_Begleitung()
    {
        await VeranstaltungAnlegenAsync();
        await AnmeldenAsync(Eingabe(begleitung: 1, infoEmails: "begleitung@example.org"));
        var bestaetigungsToken = TokenAus((await MailsAsync()).Single(), VeranstaltungLinks.Bestaetigen);

        (await Service.BestaetigungslinkPruefenAsync(bestaetigungsToken, Abbruch)).ShouldBe("Herbstseminar");
        (await Service.BestaetigenAsync(bestaetigungsToken, Basis, Abbruch)).ShouldBe(BestaetigungsErgebnis.Bestaetigt);

        var a = await AnmeldungLadenAsync();
        a.Status.ShouldBe(AnmeldungStatus.Angemeldet);
        a.EmailBestaetigtUtc.ShouldBe(Start.UtcDateTime);
        a.ReserviertBisUtc.ShouldBeNull();
        a.Ereignisse.Select(e => e.Art).ShouldBe([AnmeldungEreignisArt.Angelegt, AnmeldungEreignisArt.Bestaetigt], ignoreOrder: true);

        var mails = await MailsAsync();
        var bestaetigung = mails.Single(m => m.An == "max@example.org" && m.Betreff.StartsWith("Anmeldung bestätigt"));
        AnmeldeToken.Hash(TokenAus(bestaetigung, VeranstaltungLinks.MeineAnmeldung)).ShouldBe(a.TokenHash);
        var info = mails.Single(m => m.An == "begleitung@example.org");
        info.Prioritaet.ShouldBe(EmailPrioritaet.Normal);
        AnmeldeToken.Hash(TokenAus(info, VeranstaltungLinks.InfoAbmelden)).ShouldBe(a.InfoEmails.Single().AbmeldeTokenHash);

        // Der Bestätigungslink gilt nur einmal
        (await Service.BestaetigungslinkPruefenAsync(bestaetigungsToken, Abbruch)).ShouldBeNull();
        (await Service.BestaetigenAsync(bestaetigungsToken, Basis, Abbruch)).ShouldBe(BestaetigungsErgebnis.LinkUngueltig);
    }

    [DatenbankFact]
    public async Task Ungueltiger_oder_unbekannter_Link()
    {
        await VeranstaltungAnlegenAsync();

        (await Service.BestaetigenAsync("kaputt!", Basis, Abbruch)).ShouldBe(BestaetigungsErgebnis.LinkUngueltig);
        (await Service.BestaetigenAsync(AnmeldeToken.Erzeugen().Klartext, Basis, Abbruch)).ShouldBe(BestaetigungsErgebnis.LinkUngueltig);
        (await Service.BestaetigungslinkPruefenAsync("kaputt!", Abbruch)).ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Ohne_Double_Opt_In_sofort_angemeldet()
    {
        await VeranstaltungAnlegenAsync(doubleOptIn: false);

        (await AnmeldenAsync(Eingabe(begleitung: 1, infoEmails: "begleitung@example.org"))).ShouldBe(AnmeldeErgebnisArt.EmailVersendet);

        var a = await AnmeldungLadenAsync();
        a.Status.ShouldBe(AnmeldungStatus.Angemeldet);
        a.ReserviertBisUtc.ShouldBeNull();
        var mails = await MailsAsync();
        mails.Select(m => m.An).ShouldBe(["max@example.org", "begleitung@example.org"], ignoreOrder: true);
        AnmeldeToken.Hash(TokenAus(mails.Single(m => m.An == "max@example.org"), VeranstaltungLinks.MeineAnmeldung)).ShouldBe(a.TokenHash);
    }

    [DatenbankFact]
    public async Task Bereits_Angemeldete_bekommen_einen_neuen_Link_ohne_dass_sich_Daten_aendern()
    {
        await VeranstaltungAnlegenAsync(doubleOptIn: false);
        await AnmeldenAsync(Eingabe());
        var vorher = await AnmeldungLadenAsync();

        (await AnmeldenAsync(Eingabe(email: "MAX@example.org", vorname: "Moritz", begleitung: 3))).ShouldBe(AnmeldeErgebnisArt.EmailVersendet);

        var nachher = await AnmeldungLadenAsync();
        nachher.Vorname.ShouldBe("Max");
        nachher.AnzahlBegleitpersonen.ShouldBe(0);
        nachher.TokenHash.ShouldNotBe(vorher.TokenHash, "alter Link ist ungültig");
        nachher.Ereignisse.Select(e => e.Art).ShouldContain(AnmeldungEreignisArt.LinkVersendet);
        var linkMail = (await MailsAsync()).Last();
        AnmeldeToken.Hash(TokenAus(linkMail, VeranstaltungLinks.MeineAnmeldung)).ShouldBe(nachher.TokenHash);
    }

    [DatenbankFact]
    public async Task Abgelehnte_Adresse_bleibt_gesperrt_und_bekommt_nur_eine_neutrale_Mail()
    {
        await VeranstaltungAnlegenAsync();
        await AnmeldenAsync(Eingabe());
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var a = await kontext.Anmeldungen.SingleAsync(Abbruch);
            a.Status = AnmeldungStatus.Abgelehnt;
            await kontext.SaveChangesAsync(Abbruch);
        }

        (await AnmeldenAsync(Eingabe(vorname: "Neu"))).ShouldBe(AnmeldeErgebnisArt.EmailVersendet, "keine Auskunft über den Status");

        var nachher = await AnmeldungLadenAsync();
        nachher.Status.ShouldBe(AnmeldungStatus.Abgelehnt);
        nachher.Vorname.ShouldBe("Max");
        (await MailsAsync()).Last().Html.ShouldContain("nicht möglich");
    }

    [DatenbankFact]
    public async Task Nach_Abmeldung_wird_derselbe_Datensatz_reaktiviert()
    {
        await VeranstaltungAnlegenAsync();
        await AnmeldenAsync(Eingabe());
        var vorher = await AnmeldungLadenAsync();
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var a = await kontext.Anmeldungen.SingleAsync(Abbruch);
            a.Status = AnmeldungStatus.Storniert;
            await kontext.SaveChangesAsync(Abbruch);
        }

        (await AnmeldenAsync(Eingabe(begleitung: 2))).ShouldBe(AnmeldeErgebnisArt.EmailVersendet);

        var nachher = await AnmeldungLadenAsync();
        nachher.Id.ShouldBe(vorher.Id);
        nachher.Status.ShouldBe(AnmeldungStatus.Unbestaetigt);
        nachher.AnzahlBegleitpersonen.ShouldBe(2);
        nachher.Ereignisse.Select(e => e.Art).ShouldContain(AnmeldungEreignisArt.Reaktiviert);
    }

    [DatenbankFact]
    public async Task Reservierungen_belegen_Plaetze_bis_sie_ablaufen()
    {
        await VeranstaltungAnlegenAsync(plaetze: 3);
        await AnmeldenAsync(Eingabe("erste@example.org", begleitung: 2)); // 3 Plätze reserviert
        var ersterLink = TokenAus((await MailsAsync()).Single(), VeranstaltungLinks.Bestaetigen);

        var voll = await Service.AnmeldenAsync("herbstseminar", Eingabe("zweite@example.org"), Basis, Abbruch);
        voll.Art.ShouldBe(AnmeldeErgebnisArt.Ausgebucht);
        voll.VolleTage.ShouldBe([new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)]);

        _zeit.Advance(TimeSpan.FromHours(25)); // Reservierung abgelaufen
        (await AnmeldenAsync(Eingabe("zweite@example.org"))).ShouldBe(AnmeldeErgebnisArt.EmailVersendet);

        // Die erste Anmeldung kann jetzt nur noch bestätigt werden, wenn Platz ist – es ist keiner mehr
        (await Service.BestaetigenAsync(ersterLink, Basis, Abbruch)).ShouldBe(BestaetigungsErgebnis.Ausgebucht);
        (await AnmeldungLadenAsync("erste@example.org")).Status.ShouldBe(AnmeldungStatus.Unbestaetigt);
    }

    [DatenbankFact]
    public async Task Abgelaufene_Reservierung_kann_bestaetigt_werden_wenn_noch_Platz_ist()
    {
        await VeranstaltungAnlegenAsync(plaetze: 10);
        await AnmeldenAsync(Eingabe());
        var link = TokenAus((await MailsAsync()).Single(), VeranstaltungLinks.Bestaetigen);

        _zeit.Advance(TimeSpan.FromHours(30));

        (await Service.BestaetigenAsync(link, Basis, Abbruch)).ShouldBe(BestaetigungsErgebnis.Bestaetigt);
    }

    [DatenbankFact]
    public async Task Einzelne_Tage_werden_gespeichert_und_einzeln_gezaehlt()
    {
        var v = await VeranstaltungAnlegenAsync(plaetze: 1, modus: Teilnahmemodus.EinzelneTage, doubleOptIn: false);
        var samstag = v.Tage.Single(t => t.Datum.Day == 14).Id;
        var sonntag = v.Tage.Single(t => t.Datum.Day == 15).Id;

        var nurSamstag = Eingabe("a@example.org");
        nurSamstag.TagIds = [samstag];
        (await AnmeldenAsync(nurSamstag)).ShouldBe(AnmeldeErgebnisArt.EmailVersendet);
        (await AnmeldungLadenAsync("a@example.org")).Tage.Select(t => t.VeranstaltungsTagId).ShouldBe([samstag]);

        var nurSonntag = Eingabe("b@example.org");
        nurSonntag.TagIds = [sonntag];
        (await AnmeldenAsync(nurSonntag)).ShouldBe(AnmeldeErgebnisArt.EmailVersendet, "Sonntag ist noch frei");

        var beideTage = Eingabe("c@example.org");
        beideTage.TagIds = [samstag, sonntag];
        (await AnmeldenAsync(beideTage)).ShouldBe(AnmeldeErgebnisArt.Ausgebucht);
    }

    [DatenbankFact]
    public async Task Unbestaetigte_Anmeldung_erneut_absenden_uebernimmt_die_neuen_Daten()
    {
        var v = await VeranstaltungAnlegenAsync(modus: Teilnahmemodus.EinzelneTage);
        var samstag = v.Tage.Single(t => t.Datum.Day == 14).Id;
        var sonntag = v.Tage.Single(t => t.Datum.Day == 15).Id;
        var erste = Eingabe(begleitung: 1, infoEmails: "a@example.org");
        erste.TagIds = [samstag];
        await AnmeldenAsync(erste);

        // Samstag bleibt, Sonntag kommt dazu; Info-Adresse wechselt
        var zweite = Eingabe(vorname: "Maximilian", begleitung: 1, infoEmails: "b@example.org");
        zweite.TagIds = [samstag, sonntag];
        (await AnmeldenAsync(zweite)).ShouldBe(AnmeldeErgebnisArt.EmailVersendet);

        var a = await AnmeldungLadenAsync();
        a.Status.ShouldBe(AnmeldungStatus.Unbestaetigt);
        a.Vorname.ShouldBe("Maximilian");
        a.Tage.Select(t => t.VeranstaltungsTagId).ShouldBe([samstag, sonntag], ignoreOrder: true);
        a.InfoEmails.Select(i => i.Email).ShouldBe(["b@example.org"]);
        a.Ereignisse.Select(e => e.Art).ShouldBe([AnmeldungEreignisArt.Angelegt, AnmeldungEreignisArt.DatenGeaendert], ignoreOrder: true);
        (await MailsAsync()).Count.ShouldBe(2, "zwei Bestätigungslinks, nur der letzte gilt");
    }

    [DatenbankFact]
    public async Task Entwuerfe_geschlossene_Anmeldung_und_ungueltige_Eingaben()
    {
        await VeranstaltungAnlegenAsync(status: VeranstaltungStatus.Entwurf);
        (await AnmeldenAsync(Eingabe())).ShouldBe(AnmeldeErgebnisArt.NichtGefunden);

        await using (var kontext = Datenbank.NeuerKontext())
        {
            var v = await kontext.Veranstaltungen.SingleAsync(Abbruch);
            v.Status = VeranstaltungStatus.Veroeffentlicht;
            v.AnmeldungBis = new DateTime(2026, 9, 30); // schon vorbei
            await kontext.SaveChangesAsync(Abbruch);
        }
        (await AnmeldenAsync(Eingabe())).ShouldBe(AnmeldeErgebnisArt.Geschlossen);

        await using (var kontext = Datenbank.NeuerKontext())
        {
            var v = await kontext.Veranstaltungen.SingleAsync(Abbruch);
            v.AnmeldungBis = null;
            await kontext.SaveChangesAsync(Abbruch);
        }
        var ungueltig = await Service.AnmeldenAsync("herbstseminar", new AnmeldeEingabe { Email = "kaputt" }, Basis, Abbruch);
        ungueltig.Art.ShouldBe(AnmeldeErgebnisArt.Ungueltig);
        ungueltig.Fehler.ShouldContainKey(nameof(AnmeldeEingabe.Email));

        await using (var kontext = Datenbank.NeuerKontext())
            (await kontext.Anmeldungen.CountAsync(Abbruch)).ShouldBe(0);
        (await MailsAsync()).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Gleichzeitige_Anmeldungen_ueberbuchen_nicht()
    {
        await VeranstaltungAnlegenAsync(plaetze: 5);

        var ergebnisse = await Task.WhenAll(Enumerable.Range(1, 20)
            .Select(i => Task.Run(() => AnmeldenAsync(Eingabe($"person{i}@example.org")), Abbruch)));

        ergebnisse.Count(e => e == AnmeldeErgebnisArt.EmailVersendet).ShouldBe(5);
        ergebnisse.Count(e => e == AnmeldeErgebnisArt.Ausgebucht).ShouldBe(15);
        await using var kontext = Datenbank.NeuerKontext();
        (await kontext.Anmeldungen.CountAsync(Abbruch)).ShouldBe(5);
    }
}
