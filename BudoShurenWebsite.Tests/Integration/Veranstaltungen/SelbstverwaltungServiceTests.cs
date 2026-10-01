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
public class SelbstverwaltungServiceTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Basis = "https://test.example/";
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _zeit = new(Start);

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private SelbstverwaltungService Service => new(
        new TestKontextFabrik(Datenbank),
        new AnmeldungMailVersand(new EmailWarteschlange(_zeit, new EmailVersandSignal())),
        _zeit,
        Options.Create(new VeranstaltungenOptionen { MaxInfoEmails = 5 }));

    private async Task<Veranstaltung> VeranstaltungAnlegenAsync(
        string slug = "herbstseminar", int? plaetze = 20, Teilnahmemodus modus = Teilnahmemodus.NurGesamt,
        DateTime? aenderungenBis = null, DateOnly? ersterTag = null)
    {
        var tag1 = ersterTag ?? new DateOnly(2026, 11, 14);
        var v = new Veranstaltung
        {
            Titel = $"Titel {slug}",
            Slug = slug,
            Status = VeranstaltungStatus.Veroeffentlicht,
            KontaktEmail = "seminar@example.org",
            Teilnahmemodus = modus,
            MaxBegleitpersonen = 3,
            AenderungenBis = aenderungenBis,
            ErstelltUtc = Start.UtcDateTime,
            Tage =
            {
                new VeranstaltungsTag { Datum = tag1, Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = plaetze },
                new VeranstaltungsTag { Datum = tag1.AddDays(1), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0), MaxTeilnehmer = plaetze }
            }
        };
        await using var kontext = Datenbank.NeuerKontext();
        kontext.Veranstaltungen.Add(v);
        await kontext.SaveChangesAsync(Abbruch);
        return v;
    }

    /// <summary>Legt eine bestätigte Anmeldung an und liefert den Klartext des Verwaltungslinks.</summary>
    private async Task<string> AngemeldetAsync(
        Veranstaltung v, string email = "max@example.org", int begleitung = 0, int[]? tagIds = null,
        AnmeldungStatus status = AnmeldungStatus.Angemeldet, params AnmeldungInfoEmail[] infoEmails)
    {
        var token = AnmeldeToken.Erzeugen();
        var a = new Anmeldung
        {
            VeranstaltungId = v.Id,
            Email = email,
            Vorname = "Max",
            Nachname = "Muster",
            AnzahlBegleitpersonen = begleitung,
            Status = status,
            TokenHash = token.Hash,
            ReserviertBisUtc = status == AnmeldungStatus.Unbestaetigt ? Start.UtcDateTime.AddHours(24) : null,
            ErstelltUtc = Start.UtcDateTime
        };
        foreach (var tagId in tagIds ?? [])
            a.Tage.Add(new AnmeldungTag { VeranstaltungsTagId = tagId });
        foreach (var info in infoEmails)
            a.InfoEmails.Add(info);

        await using var kontext = Datenbank.NeuerKontext();
        kontext.Anmeldungen.Add(a);
        await kontext.SaveChangesAsync(Abbruch);
        return token.Klartext;
    }

    private static AnmeldungInfoEmail Info(string email, DateTime? abgemeldet = null) =>
        new() { Email = email, AbmeldeTokenHash = AnmeldeToken.Erzeugen().Hash, AbgemeldetUtc = abgemeldet };

    private async Task<Anmeldung> AnmeldungAsync(string email = "max@example.org")
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

    private static AnmeldeEingabe AlsEingabe(MeineAnmeldungAnsicht ansicht) => new()
    {
        Vorname = ansicht.Daten.Vorname,
        Nachname = ansicht.Daten.Nachname,
        Email = ansicht.Daten.Email,
        Telefon = ansicht.Daten.Telefon,
        Verein = ansicht.Daten.Verein,
        Graduierung = ansicht.Daten.Graduierung,
        Bemerkung = ansicht.Daten.Bemerkung,
        AnzahlBegleitpersonen = ansicht.Daten.AnzahlBegleitpersonen,
        TagIds = [.. ansicht.Daten.TagIds],
        InfoEmails = ansicht.Daten.InfoEmails
    };

    [DatenbankFact]
    public async Task Laden_zeigt_Daten_und_freie_Plaetze_ohne_die_eigenen()
    {
        var v = await VeranstaltungAnlegenAsync(plaetze: 5);
        var token = await AngemeldetAsync(v, begleitung: 2, infoEmails: [Info("b@example.org", Start.UtcDateTime), Info("a@example.org")]);
        await AngemeldetAsync(v, "andere@example.org", begleitung: 1);

        var ansicht = (await Service.LadenAsync(token, Abbruch)).ShouldNotBeNull();

        ansicht.Titel.ShouldBe("Titel herbstseminar");
        ansicht.Status.ShouldBe(AnmeldungStatus.Angemeldet);
        ansicht.AenderungenMoeglich.ShouldBeTrue();
        ansicht.AbmeldenMoeglich.ShouldBeTrue();
        ansicht.Daten.AnzahlBegleitpersonen.ShouldBe(2);
        ansicht.Daten.InfoEmails.ShouldBe("a@example.org\nb@example.org");
        ansicht.AbgemeldeteInfoEmails.ShouldBe(["b@example.org"]);
        ansicht.Tage.Select(t => t.FreiePlaetze).ShouldBe([3, 3], "5 Plätze minus 2 der anderen Anmeldung; die eigenen 3 zählen als frei");

        (await Service.LadenAsync(AnmeldeToken.Erzeugen().Klartext, Abbruch)).ShouldBeNull();
        (await Service.LadenAsync("kaputt", Abbruch)).ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Aendern_schreibt_je_Art_ein_Ereignis_und_bestaetigt_per_Mail()
    {
        var v = await VeranstaltungAnlegenAsync(modus: Teilnahmemodus.EinzelneTage);
        var samstag = v.Tage.Single(t => t.Datum.Day == 14).Id;
        var sonntag = v.Tage.Single(t => t.Datum.Day == 15).Id;
        var token = await AngemeldetAsync(v, tagIds: [samstag]);
        var eingabe = AlsEingabe((await Service.LadenAsync(token, Abbruch))!);
        eingabe.Vorname = "Moritz";
        eingabe.AnzahlBegleitpersonen = 1;
        eingabe.TagIds = [samstag, sonntag];

        var ergebnis = await Service.AendernAsync(token, eingabe, Basis, Abbruch);

        ergebnis.Art.ShouldBe(SelbstverwaltungErgebnisArt.Gespeichert);
        ergebnis.EmailWechselAngefordert.ShouldBeFalse();
        var a = await AnmeldungAsync();
        a.Vorname.ShouldBe("Moritz");
        a.Tage.Select(t => t.VeranstaltungsTagId).ShouldBe([samstag, sonntag], ignoreOrder: true);
        a.Ereignisse.Select(e => e.Art).ShouldBe(
            [AnmeldungEreignisArt.DatenGeaendert, AnmeldungEreignisArt.BegleitungGeaendert, AnmeldungEreignisArt.TageGeaendert], ignoreOrder: true);
        a.Ereignisse.ShouldAllBe(e => e.Akteur == EreignisAkteur.Teilnehmer && e.DetailsJson != null);
        a.Ereignisse.Single(e => e.Art == AnmeldungEreignisArt.DatenGeaendert).DetailsJson!.ShouldContain("\"Neu\":\"Moritz\"");
        (await MailsAsync()).ShouldHaveSingleItem().Betreff.ShouldStartWith("Anmeldung geändert");
    }

    [DatenbankFact]
    public async Task Ohne_Aenderung_passiert_nichts()
    {
        var v = await VeranstaltungAnlegenAsync();
        var token = await AngemeldetAsync(v);

        var ergebnis = await Service.AendernAsync(token, AlsEingabe((await Service.LadenAsync(token, Abbruch))!), Basis, Abbruch);

        ergebnis.Art.ShouldBe(SelbstverwaltungErgebnisArt.KeineAenderung);
        (await AnmeldungAsync()).Ereignisse.ShouldBeEmpty();
        (await MailsAsync()).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Umbuchen_ueber_die_Kapazitaet_wird_abgelehnt()
    {
        var v = await VeranstaltungAnlegenAsync(plaetze: 4);
        var token = await AngemeldetAsync(v, begleitung: 1);            // 2 Personen
        await AngemeldetAsync(v, "andere@example.org", begleitung: 1);  // 2 Personen
        var eingabe = AlsEingabe((await Service.LadenAsync(token, Abbruch))!);
        eingabe.AnzahlBegleitpersonen = 2;                              // wären 5

        var ergebnis = await Service.AendernAsync(token, eingabe, Basis, Abbruch);

        ergebnis.Art.ShouldBe(SelbstverwaltungErgebnisArt.Ausgebucht);
        ergebnis.VolleTage.Count.ShouldBe(2);
        (await AnmeldungAsync()).AnzahlBegleitpersonen.ShouldBe(1);
    }

    [DatenbankFact]
    public async Task Neue_Info_Adresse_bekommt_eine_Mail_abgemeldete_bleibt_abgemeldet()
    {
        var v = await VeranstaltungAnlegenAsync();
        var token = await AngemeldetAsync(v, begleitung: 2, infoEmails: Info("alt@example.org", Start.UtcDateTime));
        var eingabe = AlsEingabe((await Service.LadenAsync(token, Abbruch))!);
        eingabe.InfoEmails = "alt@example.org\nneu@example.org";

        (await Service.AendernAsync(token, eingabe, Basis, Abbruch)).Art.ShouldBe(SelbstverwaltungErgebnisArt.Gespeichert);

        var a = await AnmeldungAsync();
        a.InfoEmails.Single(i => i.Email == "alt@example.org").AbgemeldetUtc.ShouldNotBeNull();
        var mails = await MailsAsync();
        mails.Where(m => m.An != "max@example.org").Select(m => m.An).ShouldBe(["neu@example.org"]);
        AnmeldeToken.Hash(TokenAus(mails.Single(m => m.An == "neu@example.org"), VeranstaltungLinks.InfoAbmelden))
            .ShouldBe(a.InfoEmails.Single(i => i.Email == "neu@example.org").AbmeldeTokenHash);
    }

    [DatenbankFact]
    public async Task Neue_E_Mail_Adresse_gilt_erst_nach_Bestaetigung()
    {
        var v = await VeranstaltungAnlegenAsync();
        var alterLink = await AngemeldetAsync(v);
        var eingabe = AlsEingabe((await Service.LadenAsync(alterLink, Abbruch))!);
        eingabe.Email = "Neu@Example.org";

        var ergebnis = await Service.AendernAsync(alterLink, eingabe, Basis, Abbruch);

        ergebnis.EmailWechselAngefordert.ShouldBeTrue();
        var a = await AnmeldungAsync();
        a.NeueEmail.ShouldBe("neu@example.org");
        var mails = await MailsAsync();
        mails.Select(m => m.An).ShouldBe(["neu@example.org", "max@example.org"], ignoreOrder: true);
        mails.Single(m => m.An == "max@example.org").Html.ShouldContain("neu@example.org");
        var wechselToken = TokenAus(mails.Single(m => m.An == "neu@example.org"), VeranstaltungLinks.EmailBestaetigen);
        (await Service.EmailWechselPruefenAsync(wechselToken, Abbruch)).ShouldBe("neu@example.org");

        (await Service.EmailWechselBestaetigenAsync(wechselToken, Basis, Abbruch)).ShouldBe(EmailWechselErgebnis.Bestaetigt);

        var nachher = await AnmeldungAsync("neu@example.org");
        nachher.NeueEmail.ShouldBeNull();
        nachher.Ereignisse.Single(e => e.Art == AnmeldungEreignisArt.EmailGeaendert).DetailsJson!.ShouldContain("max@example.org");
        (await Service.LadenAsync(alterLink, Abbruch)).ShouldBeNull("der alte Link lag im alten Postfach");
        var neuerLink = TokenAus((await MailsAsync()).Last(), VeranstaltungLinks.MeineAnmeldung);
        (await Service.LadenAsync(neuerLink, Abbruch))!.Email.ShouldBe("neu@example.org");
        (await Service.EmailWechselBestaetigenAsync(wechselToken, Basis, Abbruch)).ShouldBe(EmailWechselErgebnis.LinkUngueltig, "nur einmal");
    }

    [DatenbankFact]
    public async Task Wechsel_auf_eine_schon_angemeldete_Adresse_ist_nicht_moeglich()
    {
        var v = await VeranstaltungAnlegenAsync();
        var token = await AngemeldetAsync(v);
        await AngemeldetAsync(v, "andere@example.org");
        var eingabe = AlsEingabe((await Service.LadenAsync(token, Abbruch))!);
        eingabe.Email = "andere@example.org";

        var ergebnis = await Service.AendernAsync(token, eingabe, Basis, Abbruch);

        ergebnis.Art.ShouldBe(SelbstverwaltungErgebnisArt.Ungueltig);
        ergebnis.Fehler.ShouldContainKey(nameof(AnmeldeEingabe.Email));
    }

    [DatenbankFact]
    public async Task Nach_der_Aenderungsfrist_nur_noch_abmelden()
    {
        var v = await VeranstaltungAnlegenAsync(aenderungenBis: new DateTime(2026, 9, 30));
        var token = await AngemeldetAsync(v);
        var ansicht = (await Service.LadenAsync(token, Abbruch))!;
        ansicht.AenderungenMoeglich.ShouldBeFalse();
        ansicht.AbmeldenMoeglich.ShouldBeTrue();

        var eingabe = AlsEingabe(ansicht);
        eingabe.Vorname = "Moritz";
        (await Service.AendernAsync(token, eingabe, Basis, Abbruch)).Art.ShouldBe(SelbstverwaltungErgebnisArt.NichtMehrMoeglich);
        (await Service.AbmeldenAsync(token, Basis, Abbruch)).Art.ShouldBe(SelbstverwaltungErgebnisArt.Abgemeldet);
    }

    [DatenbankFact]
    public async Task Abmelden_storniert_einmalig_und_bestaetigt_per_Mail()
    {
        var v = await VeranstaltungAnlegenAsync();
        var token = await AngemeldetAsync(v);

        (await Service.AbmeldenAsync(token, Basis, Abbruch)).Art.ShouldBe(SelbstverwaltungErgebnisArt.Abgemeldet);
        (await Service.AbmeldenAsync(token, Basis, Abbruch)).Art.ShouldBe(SelbstverwaltungErgebnisArt.Abgemeldet, "doppelt abschicken ist kein Fehler");

        var a = await AnmeldungAsync();
        a.Status.ShouldBe(AnmeldungStatus.Storniert);
        a.Ereignisse.ShouldHaveSingleItem().Art.ShouldBe(AnmeldungEreignisArt.Storniert);
        var mail = (await MailsAsync()).ShouldHaveSingleItem();
        mail.Betreff.ShouldStartWith("Abmeldung bestätigt");
        mail.Html.ShouldContain($"{Basis}veranstaltungen/herbstseminar");
        (await Service.LadenAsync(token, Abbruch))!.AbmeldenMoeglich.ShouldBeFalse();
    }

    [DatenbankFact]
    public async Task Nach_Beginn_kein_Abmelden_mehr()
    {
        var v = await VeranstaltungAnlegenAsync();
        var token = await AngemeldetAsync(v);
        _zeit.SetUtcNow(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero)); // 11:00 Ortszeit, nach Beginn

        (await Service.AbmeldenAsync(token, Basis, Abbruch)).Art.ShouldBe(SelbstverwaltungErgebnisArt.NichtMehrMoeglich);
        (await AnmeldungAsync()).Status.ShouldBe(AnmeldungStatus.Angemeldet);
    }

    [DatenbankFact]
    public async Task Links_anfordern_schickt_alle_aktiven_Anmeldungen_in_einer_Mail()
    {
        var herbst = await VeranstaltungAnlegenAsync("herbst");
        var winter = await VeranstaltungAnlegenAsync("winter", ersterTag: new DateOnly(2026, 12, 5));
        var vorbei = await VeranstaltungAnlegenAsync("vorbei", ersterTag: new DateOnly(2026, 9, 1));
        await AngemeldetAsync(herbst);
        await AngemeldetAsync(winter, status: AnmeldungStatus.Unbestaetigt);
        await AngemeldetAsync(vorbei);

        await Service.LinksAnfordernAsync(" MAX@example.org ", Basis, Abbruch);

        var mail = (await MailsAsync()).ShouldHaveSingleItem();
        mail.An.ShouldBe("max@example.org");
        mail.Html.ShouldContain("Titel herbst");
        mail.Html.ShouldContain("Titel winter");
        mail.Html.ShouldNotContain("Titel vorbei");
        await using var kontext = Datenbank.NeuerKontext();
        var anmeldungen = await kontext.Anmeldungen.AsNoTracking().Include(a => a.Veranstaltung).ToListAsync(Abbruch);
        AnmeldeToken.Hash(TokenAus(mail, VeranstaltungLinks.MeineAnmeldung)).ShouldBe(anmeldungen.Single(a => a.Veranstaltung!.Slug == "herbst").TokenHash);
        AnmeldeToken.Hash(TokenAus(mail, VeranstaltungLinks.Bestaetigen)).ShouldBe(anmeldungen.Single(a => a.Veranstaltung!.Slug == "winter").TokenHash);
    }

    [DatenbankFact]
    public async Task Links_anfordern_fuer_unbekannte_oder_ungueltige_Adressen_verschickt_nichts()
    {
        var v = await VeranstaltungAnlegenAsync();
        await AngemeldetAsync(v);

        await Service.LinksAnfordernAsync("unbekannt@example.org", Basis, Abbruch);
        await Service.LinksAnfordernAsync("kaputt", Basis, Abbruch);
        await Service.LinksAnfordernAsync(null, Basis, Abbruch);

        (await MailsAsync()).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Info_Adresse_kann_sich_selbst_abmelden()
    {
        var v = await VeranstaltungAnlegenAsync();
        var abmelden = AnmeldeToken.Erzeugen();
        await AngemeldetAsync(v, begleitung: 1, infoEmails: new AnmeldungInfoEmail { Email = "b@example.org", AbmeldeTokenHash = abmelden.Hash });

        (await Service.InfoAbmeldungPruefenAsync(abmelden.Klartext, Abbruch)).ShouldBe("Titel herbstseminar");
        (await Service.InfoAbmeldenAsync(abmelden.Klartext, Abbruch)).ShouldBeTrue();
        (await Service.InfoAbmeldenAsync(abmelden.Klartext, Abbruch)).ShouldBeTrue("zweimal ist kein Fehler");

        var a = await AnmeldungAsync();
        a.InfoEmails.Single().AbgemeldetUtc.ShouldBe(Start.UtcDateTime);
        var ereignis = a.Ereignisse.ShouldHaveSingleItem();
        ereignis.Art.ShouldBe(AnmeldungEreignisArt.InfoEmailsGeaendert);
        ereignis.Akteur.ShouldBe(EreignisAkteur.System);
        (await Service.InfoAbmeldungPruefenAsync(abmelden.Klartext, Abbruch)).ShouldBeNull("schon abgemeldet");
        (await Service.InfoAbmeldenAsync("kaputt", Abbruch)).ShouldBeFalse();
    }
}
