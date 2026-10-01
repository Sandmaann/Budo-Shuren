using System.Security.Cryptography;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration.Veranstaltungen;

[Trait("Category", "Integration")]
public class VeranstaltungVerwaltungServiceTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    // 01.10.2026 12:00 Ortszeit (Sommerzeit)
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private TestWebAppFactory? _app;
    private IServiceScope? _scope;
    private VerwaltungsBenutzer _admin = null!;
    private VerwaltungsBenutzer _aikidoLeiter = null!;

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        if (!TestDatenbank.Verfuegbar)
            return;

        // Die App wird nur für UserManager und die Rollen (Seeding beim Start) gebraucht
        _app = new TestWebAppFactory(Datenbank.Verbindung);
        _scope = _app.Services.CreateScope();

        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.Abteilungen.AddRange(
                new Abteilung { ID = "Aikido", Name = "Aikido", Email = "aikido@example.org" },
                new Abteilung { ID = "Bujinkan", Name = "Bujinkan" });
            await kontext.SaveChangesAsync(Abbruch);
        }

        _admin = await BenutzerAnlegenAsync("admin", Roles.Admin, abteilung: "");
        _aikidoLeiter = await BenutzerAnlegenAsync("leiter", Roles.Abteilungsleiter, abteilung: "Aikido");
    }

    public override async ValueTask DisposeAsync()
    {
        _scope?.Dispose();
        if (_app is not null)
            await _app.DisposeAsync();
        await base.DisposeAsync();
    }

    private IVeranstaltungVerwaltungService Service => new VeranstaltungVerwaltungService(
        new TestKontextFabrik(Datenbank),
        _scope!.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        new SlugService(),
        _zeit);

    private async Task<VerwaltungsBenutzer> BenutzerAnlegenAsync(string name, string rolle, string abteilung)
    {
        var userManager = _scope!.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@example.org", Vorname = name, Abteilung = abteilung };
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        (await userManager.AddToRoleAsync(user, rolle)).Succeeded.ShouldBeTrue();
        return new VerwaltungsBenutzer(user.Id, name, rolle == Roles.Admin, rolle == Roles.Abteilungsleiter, string.IsNullOrEmpty(abteilung) ? null : abteilung);
    }

    private static VeranstaltungEingabe Eingabe(string titel = "Herbstseminar", string? abteilung = null) => new()
    {
        Titel = titel,
        AbteilungId = abteilung,
        KontaktEmail = "Seminar@Example.org",
        Ort = "Dojo",
        Tage =
        [
            new TagEingabe { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0), MaxTeilnehmer = 20 },
            new TagEingabe { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0), Ende = new TimeOnly(13, 0), MaxTeilnehmer = 20 }
        ]
    };

    private async Task<int> AnlegenAsync(VeranstaltungEingabe eingabe, VerwaltungsBenutzer? benutzer = null)
    {
        var ergebnis = await Service.SpeichernAsync(eingabe, benutzer ?? _admin, Abbruch);
        ergebnis.Fehler.ShouldBeEmpty();
        return ergebnis.Id!.Value;
    }

    private async Task<List<AppointmentData>> KalenderAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Appointments.AsNoTracking().OrderBy(a => a.StartTime).ToListAsync(Abbruch);
    }

    private async Task AnmeldungHinzufuegenAsync(int veranstaltungId)
    {
        await using var kontext = Datenbank.NeuerKontext();
        kontext.Anmeldungen.Add(new Anmeldung
        {
            VeranstaltungId = veranstaltungId,
            Email = "teilnehmer@example.org",
            Vorname = "Max",
            Nachname = "Muster",
            Status = AnmeldungStatus.Angemeldet,
            AnzahlBegleitpersonen = 4,
            TokenHash = RandomNumberGenerator.GetBytes(32),
            ErstelltUtc = _zeit.GetUtcNow().UtcDateTime,
            Ereignisse = { new AnmeldungEreignis { ZeitpunktUtc = _zeit.GetUtcNow().UtcDateTime, Art = AnmeldungEreignisArt.Angelegt } }
        });
        await kontext.SaveChangesAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Neue_Veranstaltung_ist_Entwurf_mit_Slug_Tagen_und_Ersteller_als_Empfaenger()
    {
        var id = await AnlegenAsync(Eingabe());

        await using var kontext = Datenbank.NeuerKontext();
        var v = await kontext.Veranstaltungen.Include(x => x.Tage).Include(x => x.BenachrichtigungEmpfaenger).SingleAsync(x => x.Id == id, Abbruch);
        v.Status.ShouldBe(VeranstaltungStatus.Entwurf);
        v.Slug.ShouldBe("herbstseminar");
        v.KontaktEmail.ShouldBe("seminar@example.org");
        v.ErstelltVon.ShouldBe(_admin.UserId);
        v.Tage.Count.ShouldBe(2);
        var ersteller = v.BenachrichtigungEmpfaenger.ShouldHaveSingleItem();
        ersteller.UserId.ShouldBe(_admin.UserId);
        ersteller.Ereignisse.ShouldBe(BenachrichtigungEreignisse.Alle);
        (await KalenderAsync()).ShouldBeEmpty("Entwürfe stehen nicht im Kalender");
    }

    [DatenbankFact]
    public async Task Gleicher_Titel_bekommt_eindeutigen_Slug()
    {
        await AnlegenAsync(Eingabe());
        var zweite = await AnlegenAsync(Eingabe());

        (await Service.EingabeLadenAsync(zweite, _admin, Abbruch))!.Slug.ShouldBe("herbstseminar-2");
    }

    [DatenbankFact]
    public async Task Slugs_fester_Seiten_sind_reserviert()
    {
        var id = await AnlegenAsync(Eingabe("Link anfordern"));

        (await Service.EingabeLadenAsync(id, _admin, Abbruch))!.Slug.ShouldBe("link-anfordern-2");
    }

    [DatenbankFact]
    public async Task Abteilungsleiter_nur_fuer_die_eigene_Abteilung()
    {
        (await Service.SpeichernAsync(Eingabe(abteilung: "Bujinkan"), _aikidoLeiter, Abbruch)).Erfolgreich.ShouldBeFalse();
        (await Service.SpeichernAsync(Eingabe(abteilung: null), _aikidoLeiter, Abbruch)).Erfolgreich.ShouldBeFalse("Gesamtverein nur für Admins");

        var eigene = await AnlegenAsync(Eingabe("Aikido-Lehrgang", "Aikido"), _aikidoLeiter);
        var fremde = await AnlegenAsync(Eingabe("Bujinkan-Seminar", "Bujinkan"));

        (await Service.ListeAsync(_aikidoLeiter, mitArchivierten: false, Abbruch)).Select(e => e.Id).ShouldBe([eigene]);
        (await Service.EingabeLadenAsync(fremde, _aikidoLeiter, Abbruch)).ShouldBeNull();
        (await Service.VeroeffentlichenAsync(fremde, _aikidoLeiter, Abbruch)).Erfolgreich.ShouldBeFalse();
        (await Service.ListeAsync(_admin, mitArchivierten: false, Abbruch)).Count.ShouldBe(2);
        (await Service.AbteilungenAsync(_aikidoLeiter, Abbruch)).Select(a => a.Id).ShouldBe(["Aikido"]);
    }

    [DatenbankFact]
    public async Task Abteilungsleiter_ohne_Abteilung_sieht_nichts()
    {
        await AnlegenAsync(Eingabe());
        var ohneAbteilung = _aikidoLeiter with { Abteilung = null };

        (await Service.ListeAsync(ohneAbteilung, mitArchivierten: true, Abbruch)).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Unvollstaendige_Veranstaltung_wird_nicht_veroeffentlicht()
    {
        var eingabe = Eingabe();
        eingabe.KontaktEmail = null;
        var id = await AnlegenAsync(eingabe);

        var ergebnis = await Service.VeroeffentlichenAsync(id, _admin, Abbruch);

        ergebnis.Fehler.ShouldHaveSingleItem().ShouldContain("Kontakt-E-Mail");
        (await Service.EingabeLadenAsync(id, _admin, Abbruch))!.Status.ShouldBe(VeranstaltungStatus.Entwurf);
    }

    [DatenbankFact]
    public async Task Veroeffentlichen_traegt_jeden_Tag_in_den_Kalender_ein()
    {
        var id = await AnlegenAsync(Eingabe(abteilung: "Aikido"));

        (await Service.VeroeffentlichenAsync(id, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var geladen = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        geladen.Status.ShouldBe(VeranstaltungStatus.Veroeffentlicht);
        geladen.WarVeroeffentlicht.ShouldBeTrue();
        var kalender = await KalenderAsync();
        kalender.Select(a => a.StartTime).ShouldBe([new DateTime(2026, 11, 14, 10, 0, 0), new DateTime(2026, 11, 15, 9, 0, 0)]);
        kalender.ShouldAllBe(a => a.VeranstaltungsTagId != null && a.Abteilung == "Aikido" && a.Subject == "Herbstseminar");
    }

    [DatenbankFact]
    public async Task Nur_per_Link_sichtbare_Veranstaltung_bleibt_aus_dem_Kalender()
    {
        var eingabe = Eingabe();
        eingabe.Sichtbarkeit = VeranstaltungSichtbarkeit.NurPerLink;
        var id = await AnlegenAsync(eingabe);

        (await Service.VeroeffentlichenAsync(id, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await KalenderAsync()).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Aenderungen_nach_Veroeffentlichung_aktualisieren_den_Kalender_und_der_Slug_bleibt()
    {
        var id = await AnlegenAsync(Eingabe());
        (await Service.VeroeffentlichenAsync(id, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var eingabe = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        eingabe.Tage[0].Beginn = new TimeOnly(11, 0);
        eingabe.Tage.RemoveAt(1);
        eingabe.Tage.Add(new TagEingabe { Datum = new DateOnly(2026, 11, 21), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(12, 0) });
        (await Service.SpeichernAsync(eingabe, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await KalenderAsync()).Select(a => a.StartTime).ShouldBe([new DateTime(2026, 11, 14, 11, 0, 0), new DateTime(2026, 11, 21, 10, 0, 0)]);

        eingabe = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        eingabe.Slug = "anderer-slug";
        (await Service.SpeichernAsync(eingabe, _admin, Abbruch)).Fehler.ShouldHaveSingleItem().ShouldContain("Slug");
    }

    [DatenbankFact]
    public async Task Mit_Anmeldungen_sind_Modus_Tag_loeschen_und_zu_kleine_Kapazitaet_gesperrt()
    {
        var id = await AnlegenAsync(Eingabe());
        await AnmeldungHinzufuegenAsync(id); // 5 Personen an beiden Tagen

        var eingabe = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        eingabe.Teilnahmemodus = Teilnahmemodus.EinzelneTage;
        eingabe.Tage.RemoveAt(1);
        eingabe.Tage[0].MaxTeilnehmer = 4;

        var fehler = (await Service.SpeichernAsync(eingabe, _admin, Abbruch)).Fehler;

        fehler.Count.ShouldBe(3);
        fehler.ShouldContain(f => f.Contains("Teilnahmemodus"));
        fehler.ShouldContain(f => f.Contains("15.11.2026") && f.Contains("absagen"));
        fehler.ShouldContain(f => f.Contains("14.11.2026") && f.Contains("Kapazität"));
    }

    [DatenbankFact]
    public async Task Gleichzeitige_Bearbeitung_wird_erkannt()
    {
        var id = await AnlegenAsync(Eingabe());
        var ersteSicht = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        var zweiteSicht = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;

        ersteSicht.Ort = "Halle";
        (await Service.SpeichernAsync(ersteSicht, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        zweiteSicht.Ort = "Park";
        (await Service.SpeichernAsync(zweiteSicht, _admin, Abbruch)).Fehler.ShouldHaveSingleItem().ShouldContain("inzwischen");
    }

    [DatenbankFact]
    public async Task Archivieren_und_Loeschen_nur_wenn_es_passt()
    {
        var entwurf = await AnlegenAsync(Eingabe("Entwurf"));
        var veroeffentlicht = await AnlegenAsync(Eingabe("Seminar"));
        (await Service.VeroeffentlichenAsync(veroeffentlicht, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await Service.ArchivierenAsync(veroeffentlicht, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("steht noch bevor");
        (await Service.LoeschenAsync(veroeffentlicht, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("war veröffentlicht");

        _zeit.Advance(TimeSpan.FromDays(60)); // Veranstaltung ist vorbei
        (await Service.ArchivierenAsync(veroeffentlicht, _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await KalenderAsync()).Count.ShouldBe(2, "vergangene Termine bleiben im Kalender");
        (await Service.ListeAsync(_admin, mitArchivierten: false, Abbruch)).Select(e => e.Id).ShouldBe([entwurf]);

        (await Service.LoeschenAsync(entwurf, _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await Service.EingabeLadenAsync(entwurf, _admin, Abbruch)).ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Liste_zaehlt_Anmeldungen_Personen_und_ungesehene_Aenderungen()
    {
        var id = await AnlegenAsync(Eingabe());
        await AnmeldungHinzufuegenAsync(id);

        var eintrag = (await Service.ListeAsync(_admin, mitArchivierten: false, Abbruch)).ShouldHaveSingleItem();

        eintrag.AktiveAnmeldungen.ShouldBe(1);
        eintrag.BestaetigtePersonen.ShouldBe(5);
        eintrag.UngeseheneAenderungen.ShouldBe(1);
        eintrag.ErsterTag.ShouldBe(new DateOnly(2026, 11, 14));
        eintrag.LetzterTag.ShouldBe(new DateOnly(2026, 11, 15));
    }

    [DatenbankFact]
    public async Task Empfaenger_hinzufuegen_aendern_und_entfernen()
    {
        var id = await AnlegenAsync(Eingabe());

        (await Service.EmpfaengerHinzufuegenAsync(id, null, " Kasse@Example.org ", "Kassenwart", _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await Service.EmpfaengerHinzufuegenAsync(id, null, "kasse@example.org", null, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("doppelt");
        (await Service.EmpfaengerHinzufuegenAsync(id, null, "keine-adresse", null, _admin, Abbruch)).Erfolgreich.ShouldBeFalse();
        (await Service.EmpfaengerHinzufuegenAsync(id, _aikidoLeiter.UserId, null, null, _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await Service.EmpfaengerHinzufuegenAsync(id, _admin.UserId, null, null, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("Ersteller ist schon eingetragen");
        (await Service.EmpfaengerHinzufuegenAsync(id, _admin.UserId, "x@example.org", null, _admin, Abbruch)).Erfolgreich.ShouldBeFalse("nicht beides");

        var empfaenger = await Service.EmpfaengerAsync(id, _admin, Abbruch);
        empfaenger.Count.ShouldBe(3);
        empfaenger[0].IstErsteller.ShouldBeTrue("Ersteller steht oben");
        var kasse = empfaenger.Single(e => e.Email == "kasse@example.org");
        kasse.Anzeigename.ShouldBe("Kassenwart");
        kasse.IstBenutzer.ShouldBeFalse();

        (await Service.EmpfaengerAendernAsync(id, kasse.Id, BenachrichtigungEreignisse.NeueAnmeldung, BenachrichtigungModus.TaeglicheZusammenfassung, _admin, Abbruch)).Fehler.ShouldBeEmpty();
        var geaendert = (await Service.EmpfaengerAsync(id, _admin, Abbruch)).Single(e => e.Id == kasse.Id);
        geaendert.Ereignisse.ShouldBe(BenachrichtigungEreignisse.NeueAnmeldung);
        geaendert.Modus.ShouldBe(BenachrichtigungModus.TaeglicheZusammenfassung);

        (await Service.EmpfaengerEntfernenAsync(id, kasse.Id, _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await Service.EmpfaengerAsync(id, _admin, Abbruch)).Count.ShouldBe(2);

        (await Service.EmpfaengerHinzufuegenAsync(id, null, "y@example.org", null, _aikidoLeiter, Abbruch)).Erfolgreich.ShouldBeFalse("Gesamtverein: nur Admins");
    }

    [DatenbankFact]
    public async Task Moegliche_Empfaenger_sind_Admins_Abteilungsleiter_und_Editoren()
    {
        await BenutzerAnlegenAsync("editor", Roles.Editor, abteilung: "Aikido");
        await BenutzerAnlegenAsync("mitglied", Roles.Mitglied, abteilung: "Aikido");

        var auswahl = await Service.MoeglicheEmpfaengerAsync(Abbruch);

        auswahl.Select(b => b.Anzeigename).OrderBy(n => n).ShouldBe(["admin", "editor", "leiter"]);
    }
}
