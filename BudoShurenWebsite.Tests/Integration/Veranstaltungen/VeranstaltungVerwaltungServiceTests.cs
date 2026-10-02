using System.Security.Cryptography;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Services.Mail;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        _zeit,
        new EmailWarteschlange(_zeit, new EmailVersandSignal()),
        Options.Create(new VeranstaltungenOptionen { WebsiteUrl = "https://test.example/" }));

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
        v.PrivateVeranstaltung.ShouldBeTrue("Hinweis \"private Veranstaltung\" ist Standard");
    }

    [DatenbankFact]
    public async Task Hinweis_private_Veranstaltung_laesst_sich_abschalten()
    {
        var id = await AnlegenAsync(Eingabe());
        var eingabe = (await Service.EingabeLadenAsync(id, _admin, Abbruch)).ShouldNotBeNull();
        eingabe.PrivateVeranstaltung.ShouldBeTrue();

        eingabe.PrivateVeranstaltung = false;
        (await Service.SpeichernAsync(eingabe, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await Service.EingabeLadenAsync(id, _admin, Abbruch))!.PrivateVeranstaltung.ShouldBeFalse();
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
    public async Task Abteilungsleiter_fuer_die_eigene_Abteilung_und_den_Gesamtverein()
    {
        (await Service.SpeichernAsync(Eingabe(abteilung: "Bujinkan"), _aikidoLeiter, Abbruch)).Erfolgreich.ShouldBeFalse();

        var eigene = await AnlegenAsync(Eingabe("Aikido-Lehrgang", "Aikido"), _aikidoLeiter);
        var verein = await AnlegenAsync(Eingabe("Sommerfest", abteilung: null), _aikidoLeiter);
        var fremde = await AnlegenAsync(Eingabe("Bujinkan-Seminar", "Bujinkan"));

        (await Service.ListeAsync(_aikidoLeiter, mitArchivierten: false, Abbruch)).Select(e => e.Id).ShouldBe([eigene, verein], ignoreOrder: true);
        (await Service.EingabeLadenAsync(verein, _aikidoLeiter, Abbruch)).ShouldNotBeNull();
        (await Service.VeroeffentlichenAsync(verein, _aikidoLeiter, Abbruch)).Erfolgreich.ShouldBeTrue();
        (await Service.EingabeLadenAsync(fremde, _aikidoLeiter, Abbruch)).ShouldBeNull();
        (await Service.VeroeffentlichenAsync(fremde, _aikidoLeiter, Abbruch)).Erfolgreich.ShouldBeFalse();
        (await Service.ListeAsync(_admin, mitArchivierten: false, Abbruch)).Count.ShouldBe(3);
        (await Service.AbteilungenAsync(_aikidoLeiter, Abbruch)).Select(a => a.Id).ShouldBe(["Aikido"]);
    }

    [DatenbankFact]
    public async Task Abteilungsleiter_ohne_Abteilung_sieht_nur_den_Gesamtverein()
    {
        await AnlegenAsync(Eingabe("Aikido-Lehrgang", "Aikido"));
        var verein = await AnlegenAsync(Eingabe("Sommerfest", abteilung: null));
        var ohneAbteilung = _aikidoLeiter with { Abteilung = null };

        (await Service.ListeAsync(ohneAbteilung, mitArchivierten: true, Abbruch)).Select(e => e.Id).ShouldBe([verein]);
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

        // Der Kalender bekommt beim Lesen Id und Slug für die Links zur Veranstaltung
        await using var kontext = Datenbank.NeuerKontext();
        var eintraege = await kontext.Appointments.ToListAsync(Abbruch);
        eintraege.Add(new AppointmentData { Subject = "Training" });
        await KalenderAbgleich.VeranstaltungenZuordnenAsync(kontext, eintraege, Abbruch);
        eintraege.Where(a => a.VeranstaltungsTagId != null).ShouldAllBe(a => a.VeranstaltungId == id && a.VeranstaltungSlug == "herbstseminar");
        eintraege.Single(a => a.Subject == "Training").VeranstaltungSlug.ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Archivierter_Entwurf_kommt_nicht_in_den_Kalender()
    {
        var id = await AnlegenAsync(Eingabe());

        (await Service.ArchivierenAsync(id, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        (await KalenderAsync()).ShouldBeEmpty("nie veröffentlicht, also auch nicht als Rückblick");
    }

    [DatenbankFact]
    public async Task Mehrere_Termine_an_einem_Datum_auch_mit_offenem_Ende()
    {
        var eingabe = Eingabe();
        eingabe.Tage.Add(new TagEingabe { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(19, 0), Ende = null, Titel = "Essen", MaxTeilnehmer = 30 });
        var id = await AnlegenAsync(eingabe);

        (await Service.VeroeffentlichenAsync(id, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var geladen = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        geladen.Tage.Select(t => (t.Datum.Day, t.Beginn.Hour, t.Ende?.Hour)).ShouldBe([(14, 10, 16), (14, 19, (int?)null), (15, 9, 13)]);
        var essen = (await KalenderAsync())[1];
        essen.Subject.ShouldBe("Herbstseminar – Essen");
        essen.EndTime.ShouldBe(new DateTime(2026, 11, 14, 19, 30, 0), "offenes Ende: kurzer Eintrag ohne erfundene Endzeit");
    }

    [DatenbankFact]
    public async Task Derselbe_Termin_zweimal_wird_abgelehnt()
    {
        var eingabe = Eingabe();
        eingabe.Tage.Add(new TagEingabe { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(12, 0) });

        var ergebnis = await Service.SpeichernAsync(eingabe, _admin, Abbruch);

        ergebnis.Fehler.ShouldHaveSingleItem().ShouldBe("Der Termin Sa 14.11. 10:00 ist doppelt angelegt.");
    }

    private static MemoryStream Png()
    {
        using var bild = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(40, 30);
        var daten = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(bild, daten);
        daten.Position = 0;
        return daten;
    }

    private async Task<int> HochladenAsync() => (await Service.BildHochladenAsync(Png(), "dojo.png", _admin, Abbruch)).Id!.Value;

    private async Task<List<int>> BildIdsAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Images.Select(i => i.Id).ToListAsync(Abbruch);
    }

    /// <summary>Bilder, die noch auf das Speichern warten (sonst räumt BildAufraeumJob sie auf).</summary>
    private async Task<List<int>> VorlaeufigeBildIdsAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.Images.Where(i => i.VorlaeufigSeitUtc != null).Select(i => i.Id).ToListAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Bausteine_werden_gespeichert_und_entfernte_Bilder_geloescht()
    {
        var (erstes, zweites, nieGespeichert) = (await HochladenAsync(), await HochladenAsync(), await HochladenAsync());
        var eingabe = Eingabe();
        eingabe.Bloecke =
        [
            new BlockEingabe { Typ = VeranstaltungBlockTyp.MarkdownText, MarkdownInhalt = "Hallo" },
            new BlockEingabe { Typ = VeranstaltungBlockTyp.BilderGalerie, BilderProReihe = 2, BildUnterschrift = " Training ", BildIds = [zweites, erstes] }
        ];
        var id = await AnlegenAsync(eingabe);
        (await VorlaeufigeBildIdsAsync()).ShouldBe([nieGespeichert], "gespeicherte Bilder sind nicht mehr vorläufig");

        var geladen = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        geladen.Bloecke.Select(b => b.Typ).ShouldBe([VeranstaltungBlockTyp.MarkdownText, VeranstaltungBlockTyp.BilderGalerie]);
        geladen.Bloecke[0].MarkdownInhalt.ShouldBe("Hallo");
        geladen.Bloecke[1].BildIds.ShouldBe([zweites, erstes]);
        geladen.Bloecke[1].BildUnterschrift.ShouldBe("Training");

        // Bild entfernen und Bausteine tauschen
        geladen.Bloecke[1].BildIds.Remove(erstes);
        geladen.Bloecke.Reverse();
        (await Service.SpeichernAsync(geladen, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        var danach = (await Service.EingabeLadenAsync(id, _admin, Abbruch))!;
        danach.Bloecke.Select(b => b.Typ).ShouldBe([VeranstaltungBlockTyp.BilderGalerie, VeranstaltungBlockTyp.MarkdownText]);
        (await BildIdsAsync()).ShouldBe([zweites, nieGespeichert], "die Bilddaten des entfernten Bildes sind gelöscht");

        (await Service.LoeschenAsync(id, _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await BildIdsAsync()).ShouldBe([nieGespeichert], "mit dem Entwurf verschwinden auch seine Bilder");
    }

    [DatenbankFact]
    public async Task Nur_frisch_hochgeladene_Bilder_koennen_eingebunden_werden()
    {
        // Ein Bild, das schon woanders verwendet wird (hier eine Neuigkeit), darf nicht über die Veranstaltung öffentlich werden
        await using (var kontext = Datenbank.NeuerKontext())
        {
            kontext.Neuigkeiten.Add(new Neuigkeit
            {
                Titel = "Intern",
                DbImage = new DbImage { Title = "intern.jpg", ImageData = [1], ContentType = "image/jpeg", CreatedAt = _zeit.GetUtcNow().UtcDateTime }
            });
            await kontext.SaveChangesAsync(Abbruch);
        }
        var fremdesBild = (await BildIdsAsync()).Single();

        // Unverwendet, aber nicht frisch hochgeladen (z. B. ein Upload der Galerie-Verwaltung): ebenfalls nicht
        int altesBild;
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var bild = new DbImage { Title = "alt.jpg", ImageData = [1], ContentType = "image/jpeg", CreatedAt = _zeit.GetUtcNow().UtcDateTime };
            kontext.Images.Add(bild);
            await kontext.SaveChangesAsync(Abbruch);
            altesBild = bild.Id;
        }

        foreach (var bildId in new[] { fremdesBild, altesBild, 999_999 })
        {
            var eingabe = Eingabe();
            eingabe.Bloecke = [new BlockEingabe { Typ = VeranstaltungBlockTyp.BilderGalerie, BildIds = [bildId] }];

            (await Service.SpeichernAsync(eingabe, _admin, Abbruch)).Fehler.ShouldHaveSingleItem().ShouldContain("gehört nicht zu dieser Veranstaltung");
        }
    }

    [DatenbankFact]
    public async Task Hochladen_speichert_JPEG_und_lehnt_andere_Dateien_ab()
    {
        var id = await HochladenAsync();
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var bild = await kontext.Images.SingleAsync(i => i.Id == id, Abbruch);
            bild.ContentType.ShouldBe("image/jpeg");
            bild.VorlaeufigSeitUtc.ShouldBe(_zeit.GetUtcNow().UtcDateTime, "bis zum Speichern der Veranstaltung");
        }

        var ergebnis = await Service.BildHochladenAsync(new MemoryStream("kein Bild"u8.ToArray()), "notiz.png", _admin, Abbruch);

        ergebnis.Fehler.ShouldHaveSingleItem().ShouldBe("„notiz.png“ ist kein unterstütztes Bild (JPG, PNG oder WebP).");
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
        fehler.ShouldContain(f => f.Contains("So 15.11.") && f.Contains("absagen"));
        fehler.ShouldContain(f => f.Contains("Sa 14.11.") && f.Contains("Kapazität"));
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
    public async Task Liste_zaehlt_Teilnehmer_und_ungesehene_Aenderungen()
    {
        var id = await AnlegenAsync(Eingabe());
        await AnmeldungHinzufuegenAsync(id);

        var eintrag = (await Service.ListeAsync(_admin, mitArchivierten: false, Abbruch)).ShouldHaveSingleItem();

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

        (await Service.EmpfaengerHinzufuegenAsync(id, null, "y@example.org", null, _aikidoLeiter, Abbruch)).Fehler.ShouldBeEmpty("Gesamtverein: auch Abteilungsleiter");
    }

    [DatenbankFact]
    public async Task Freie_Adresse_bekommt_Mail_mit_Abmeldelink_Benutzer_nicht()
    {
        var id = await AnlegenAsync(Eingabe());

        (await Service.EmpfaengerHinzufuegenAsync(id, null, "kasse@example.org", "Kassenwart", _admin, Abbruch)).Fehler.ShouldBeEmpty();
        (await Service.EmpfaengerHinzufuegenAsync(id, _aikidoLeiter.UserId, null, null, _admin, Abbruch)).Fehler.ShouldBeEmpty();

        await using var kontext = Datenbank.NeuerKontext();
        var mail = (await kontext.EmailAusgang.AsNoTracking().ToListAsync(Abbruch)).ShouldHaveSingleItem();
        mail.An.ShouldBe("kasse@example.org");
        mail.AntwortAn.ShouldBe("seminar@example.org");
        mail.Html.ShouldContain("admin hat diese Adresse");

        var token = System.Text.RegularExpressions.Regex.Match(mail.Html, "benachrichtigung-abmelden/([A-Za-z0-9_-]+)").Groups[1].Value;
        mail.Html.ShouldContain("https://test.example/veranstaltungen/benachrichtigung-abmelden/");
        var kasse = await kontext.BenachrichtigungEmpfaenger.AsNoTracking().SingleAsync(e => e.Email == "kasse@example.org", Abbruch);
        kasse.AbmeldeTokenHash.ShouldBe(AnmeldeToken.Hash(token));
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
