using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class BenachrichtigungsAuswertungTests
{
    // 01.10.2026 12:00 Ortszeit (Sommerzeit, UTC+2)
    private static readonly DateTime Jetzt = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Bis = Jetzt.AddHours(-1);

    private static EmpfaengerStand Empfaenger(
        BenachrichtigungModus modus = BenachrichtigungModus.Sofort,
        BenachrichtigungEreignisse ereignisse = BenachrichtigungEreignisse.Alle,
        string? userId = "orga",
        bool abgemeldet = false,
        DateTime? bis = null) =>
        new(modus, ereignisse, userId, abgemeldet, bis ?? Bis);

    private static VeranstaltungStand Veranstaltung(bool doubleOptIn = true, DateTime? schluss = null, bool ausgebucht = false, int[]? unbestaetigt = null) =>
        new(doubleOptIn, schluss, new TimeOnly(7, 0), ausgebucht, (unbestaetigt ?? []).ToHashSet());

    private static BenachrichtigungEreignis Ereignis(AnmeldungEreignisArt art, TimeSpan vorJetzt, int anmeldung = 1,
        EreignisAkteur akteur = EreignisAkteur.Teilnehmer, string? userId = null) =>
        new(anmeldung, Jetzt - vorJetzt, art, akteur, userId, null);

    private static BenachrichtigungsPlan? Auswerten(EmpfaengerStand e, params BenachrichtigungEreignis[] ereignisse) =>
        BenachrichtigungsAuswertung.Auswerten(e, Veranstaltung(), ereignisse, Jetzt);

    [Fact]
    public void Sofort_wartet_auf_5_Minuten_Ruhe()
    {
        var alt = Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(20));
        var frisch = Ereignis(AnmeldungEreignisArt.TageGeaendert, TimeSpan.FromMinutes(3));

        Auswerten(Empfaenger(), alt, frisch).ShouldBeNull();

        var plan = Auswerten(Empfaenger(), alt, Ereignis(AnmeldungEreignisArt.TageGeaendert, TimeSpan.FromMinutes(6))).ShouldNotBeNull();
        plan.Meldungen.Select(m => m.Art).ShouldBe([AnmeldungEreignisArt.Bestaetigt, AnmeldungEreignisArt.TageGeaendert]);
        plan.NeuBisUtc.ShouldBe(Jetzt - BenachrichtigungsAuswertung.Puffer);
        plan.Senden.ShouldBeTrue();
    }

    [Fact]
    public void Sofort_bei_Dauerbetrieb_spaetestens_nach_30_Minuten()
    {
        var plan = Auswerten(Empfaenger(),
            Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(31)),
            Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(2), anmeldung: 2),
            Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromSeconds(30), anmeldung: 3)).ShouldNotBeNull();

        // Was jünger als der Puffer ist, kommt in die nächste Mail
        plan.Meldungen.Select(m => m.AnmeldungId).ShouldBe([1, 2]);
    }

    [Fact]
    public void Ohne_Ereignisse_nichts_senden_aber_Stand_fortschreiben()
    {
        var plan = Auswerten(Empfaenger()).ShouldNotBeNull();

        plan.Senden.ShouldBeFalse();
        plan.NeuBisUtc.ShouldBe(Jetzt - BenachrichtigungsAuswertung.Puffer);
    }

    [Fact]
    public void Bereits_gemeldete_Ereignisse_kommen_nicht_noch_einmal()
    {
        var gemeldet = Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(20));

        Auswerten(Empfaenger(bis: gemeldet.ZeitpunktUtc), gemeldet).ShouldNotBeNull().Senden.ShouldBeFalse();
    }

    [Theory]
    [InlineData(BenachrichtigungModus.Pausiert, false)]
    [InlineData(BenachrichtigungModus.Sofort, true)]
    public void Pausiert_oder_abgemeldet_nichts_senden(BenachrichtigungModus modus, bool abgemeldet)
    {
        var plan = Auswerten(Empfaenger(modus, abgemeldet: abgemeldet), Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(1))).ShouldNotBeNull();

        plan.Senden.ShouldBeFalse();
        plan.NeuBisUtc.ShouldBe(Jetzt - BenachrichtigungsAuswertung.Puffer, "beim Wiedereinschalten keine alten Ereignisse");
    }

    [Fact]
    public void Eigene_Aktionen_werden_nicht_gemeldet_die_anderer_Organisatoren_schon()
    {
        var plan = Auswerten(Empfaenger(userId: "orga"),
            Ereignis(AnmeldungEreignisArt.Abgelehnt, TimeSpan.FromMinutes(10), akteur: EreignisAkteur.Admin, userId: "orga"),
            Ereignis(AnmeldungEreignisArt.Abgelehnt, TimeSpan.FromMinutes(10), anmeldung: 2, akteur: EreignisAkteur.Admin, userId: "kollegin")).ShouldNotBeNull();

        plan.Meldungen.Select(m => m.AnmeldungId).ShouldBe([2]);
    }

    [Fact]
    public void Nur_abonnierte_Ereignisse()
    {
        var plan = Auswerten(Empfaenger(ereignisse: BenachrichtigungEreignisse.Abmeldung),
            Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(10)),
            Ereignis(AnmeldungEreignisArt.Storniert, TimeSpan.FromMinutes(10), anmeldung: 2),
            Ereignis(AnmeldungEreignisArt.DatenGeaendert, TimeSpan.FromMinutes(10), anmeldung: 3)).ShouldNotBeNull();

        plan.Meldungen.Select(m => m.AnmeldungId).ShouldBe([2]);
    }

    [Theory]
    // Mit Double-Opt-In zählt erst die Bestätigung
    [InlineData(AnmeldungEreignisArt.Angelegt, EreignisAkteur.Teilnehmer, true, null)]
    [InlineData(AnmeldungEreignisArt.Reaktiviert, EreignisAkteur.Teilnehmer, true, null)]
    [InlineData(AnmeldungEreignisArt.Bestaetigt, EreignisAkteur.Teilnehmer, true, BenachrichtigungEreignisse.NeueAnmeldung)]
    // Ohne Opt-In bzw. manuell ist die Anmeldung sofort gültig
    [InlineData(AnmeldungEreignisArt.Angelegt, EreignisAkteur.Teilnehmer, false, BenachrichtigungEreignisse.NeueAnmeldung)]
    [InlineData(AnmeldungEreignisArt.Angelegt, EreignisAkteur.Admin, true, BenachrichtigungEreignisse.NeueAnmeldung)]
    [InlineData(AnmeldungEreignisArt.AblehnungZurueckgenommen, EreignisAkteur.Admin, true, BenachrichtigungEreignisse.NeueAnmeldung)]
    [InlineData(AnmeldungEreignisArt.Storniert, EreignisAkteur.Teilnehmer, true, BenachrichtigungEreignisse.Abmeldung)]
    [InlineData(AnmeldungEreignisArt.Abgelehnt, EreignisAkteur.Admin, true, BenachrichtigungEreignisse.Abmeldung)]
    [InlineData(AnmeldungEreignisArt.BegleitungGeaendert, EreignisAkteur.Teilnehmer, true, BenachrichtigungEreignisse.Aenderung)]
    [InlineData(AnmeldungEreignisArt.AdminBearbeitet, EreignisAkteur.Admin, true, BenachrichtigungEreignisse.Aenderung)]
    // Nie gemeldet
    [InlineData(AnmeldungEreignisArt.LinkVersendet, EreignisAkteur.Admin, true, null)]
    [InlineData(AnmeldungEreignisArt.TagAbgesagt, EreignisAkteur.Admin, true, null)]
    [InlineData(AnmeldungEreignisArt.Storniert, EreignisAkteur.System, true, null)]
    public void Kategorie(AnmeldungEreignisArt art, EreignisAkteur akteur, bool doubleOptIn, BenachrichtigungEreignisse? erwartet) =>
        BenachrichtigungsAuswertung.Kategorie(new BenachrichtigungEreignis(1, Jetzt, art, akteur, null, null), Veranstaltung(doubleOptIn))
            .ShouldBe(erwartet);

    [Fact]
    public void Aenderungen_unbestaetigter_Anmeldungen_interessieren_nicht() =>
        BenachrichtigungsAuswertung.Kategorie(new BenachrichtigungEreignis(7, Jetzt, AnmeldungEreignisArt.DatenGeaendert, EreignisAkteur.Teilnehmer, null, null),
            Veranstaltung(unbestaetigt: [7])).ShouldBeNull();

    [Fact]
    public void Ausgebucht_nur_nach_einer_Anmeldung_oder_Umbuchung()
    {
        var voll = Veranstaltung(ausgebucht: true);

        BenachrichtigungsAuswertung.Auswerten(Empfaenger(), voll, [Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(10))], Jetzt)!
            .Ausgebucht.ShouldBeTrue();
        BenachrichtigungsAuswertung.Auswerten(Empfaenger(), voll, [Ereignis(AnmeldungEreignisArt.DatenGeaendert, TimeSpan.FromMinutes(10))], Jetzt)!
            .Ausgebucht.ShouldBeFalse("eine Namensänderung macht die Veranstaltung nicht voll");
        BenachrichtigungsAuswertung.Auswerten(Empfaenger(ereignisse: BenachrichtigungEreignisse.NeueAnmeldung), voll,
            [Ereignis(AnmeldungEreignisArt.Bestaetigt, TimeSpan.FromMinutes(10))], Jetzt)!.Ausgebucht.ShouldBeFalse("nicht abonniert");
    }

    [Fact]
    public void Anmeldeschluss_einmal_wenn_er_ins_Fenster_faellt()
    {
        var schluss = Jetzt.AddMinutes(-30);

        var plan = BenachrichtigungsAuswertung.Auswerten(Empfaenger(), Veranstaltung(schluss: schluss), [], Jetzt).ShouldNotBeNull();
        plan.Anmeldeschluss.ShouldBeTrue();
        plan.Senden.ShouldBeTrue();

        BenachrichtigungsAuswertung.Auswerten(Empfaenger(bis: plan.NeuBisUtc), Veranstaltung(schluss: schluss), [], Jetzt.AddMinutes(1))!
            .Anmeldeschluss.ShouldBeFalse();
    }

    [Fact]
    public void Zusammenfassung_einmal_taeglich_nach_der_Uhrzeit()
    {
        var empfaenger = Empfaenger(BenachrichtigungModus.TaeglicheZusammenfassung, bis: new DateTime(2026, 9, 30, 6, 0, 0, DateTimeKind.Utc));
        var ereignis = new BenachrichtigungEreignis(1, new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc), AnmeldungEreignisArt.Bestaetigt, EreignisAkteur.Teilnehmer, null, null);
        // 07:00 Ortszeit = 05:00 UTC; der Puffer muss vorbei sein
        var vorher = new DateTime(2026, 10, 1, 5, 0, 30, DateTimeKind.Utc);
        var danach = new DateTime(2026, 10, 1, 5, 2, 0, DateTimeKind.Utc);

        BenachrichtigungsAuswertung.Auswerten(empfaenger, Veranstaltung(), [ereignis], vorher).ShouldBeNull();
        var plan = BenachrichtigungsAuswertung.Auswerten(empfaenger, Veranstaltung(), [ereignis], danach).ShouldNotBeNull();
        plan.Meldungen.Count.ShouldBe(1);

        BenachrichtigungsAuswertung.Auswerten(empfaenger with { BenachrichtigtBisUtc = plan.NeuBisUtc }, Veranstaltung(), [ereignis], danach.AddHours(5))
            .ShouldBeNull("erst am nächsten Morgen wieder");
    }

    [Fact]
    public void Zusammenfassung_ohne_Neuigkeiten_wird_nicht_gesendet()
    {
        var empfaenger = Empfaenger(BenachrichtigungModus.TaeglicheZusammenfassung, bis: new DateTime(2026, 9, 30, 6, 0, 0, DateTimeKind.Utc));
        var danach = new DateTime(2026, 10, 1, 5, 2, 0, DateTimeKind.Utc);
        // Nur Ereignisse, die nie gemeldet werden (System, unbestätigte Anmeldung)
        BenachrichtigungEreignis[] ereignisse =
        [
            new(1, new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc), AnmeldungEreignisArt.Angelegt, EreignisAkteur.Teilnehmer, null, null),
            new(2, new DateTime(2026, 9, 30, 16, 0, 0, DateTimeKind.Utc), AnmeldungEreignisArt.Storniert, EreignisAkteur.System, null, null)
        ];

        foreach (var offen in new BenachrichtigungEreignis[][] { [], ereignisse })
        {
            var plan = BenachrichtigungsAuswertung.Auswerten(empfaenger, Veranstaltung(), offen, danach).ShouldNotBeNull();
            plan.Senden.ShouldBeFalse();
            plan.NeuBisUtc.ShouldBe(danach - BenachrichtigungsAuswertung.Puffer, "der Tag gilt trotzdem als erledigt");
        }
    }

    [Theory]
    // Sommerzeit: 07:00 Ortszeit = 05:00 UTC
    [InlineData("2026-10-01T05:30:00", "2026-10-01T05:00:00")]
    [InlineData("2026-10-01T04:59:00", "2026-09-30T05:00:00")]
    // Winterzeit (nach dem 25.10.2026): 07:00 Ortszeit = 06:00 UTC
    [InlineData("2026-10-26T06:00:00", "2026-10-26T06:00:00")]
    // Tag der Umstellung: am Morgen gilt schon Winterzeit, am Vortag noch Sommerzeit
    [InlineData("2026-10-25T05:30:00", "2026-10-24T05:00:00")]
    public void Letzter_Termin_der_Zusammenfassung(string zeitpunkt, string erwartet) =>
        BenachrichtigungsAuswertung.LetzterTerminUtc(new TimeOnly(7, 0), DateTime.SpecifyKind(DateTime.Parse(zeitpunkt), DateTimeKind.Utc))
            .ShouldBe(DateTime.SpecifyKind(DateTime.Parse(erwartet), DateTimeKind.Utc));
}
