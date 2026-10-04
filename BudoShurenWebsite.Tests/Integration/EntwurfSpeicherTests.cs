using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services;
using BudoShurenWebsite.Services.Entwuerfe;
using BudoShurenWebsite.Services.Veranstaltungen;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Integration;

[Trait("Category", "Integration")]
public class EntwurfSpeicherTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    private const string Olga = "olga";
    private const string Paul = "paul";
    private const string Schluessel = "veranstaltung:neu";

    private static readonly DateTime Jetzt = new(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(Jetzt));

    private CancellationToken Abbruch => TestContext.Current.CancellationToken;

    private EntwurfSpeicher Speicher => new(new TestKontextFabrik(Datenbank), _zeit, NullLogger<EntwurfSpeicher>.Instance);

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        if (!TestDatenbank.Verfuegbar)
            return;

        await using var kontext = Datenbank.NeuerKontext();
        kontext.Users.AddRange(
            new ApplicationUser { Id = Olga, UserName = Olga, Vorname = "Olga" },
            new ApplicationUser { Id = Paul, UserName = Paul, Vorname = "Paul" });
        await kontext.SaveChangesAsync(Abbruch);
    }

    private Task SichernAsync(string benutzer, string schluessel, VeranstaltungEingabe eingabe, params int[] bildIds) =>
        Speicher.SpeichernAsync(benutzer, schluessel, EntwurfSpeicher.AlsJson(eingabe), bildIds, Abbruch);

    private async Task<List<BearbeitungsEntwurf>> EntwuerfeAsync()
    {
        await using var kontext = Datenbank.NeuerKontext();
        return await kontext.BearbeitungsEntwuerfe.AsNoTracking().OrderBy(e => e.Id).ToListAsync(Abbruch);
    }

    [DatenbankFact]
    public async Task Gesicherter_Stand_kommt_vollstaendig_zurueck()
    {
        var eingabe = new VeranstaltungEingabe
        {
            Id = 7,
            Titel = "Herbstseminar „Kihon“",
            AnmeldungBis = new DateTime(2026, 11, 1, 18, 0, 0),
            RowVersion = [1, 2, 3, 4, 5, 6, 7, 8],
            ZusammenfassungUhrzeit = new TimeOnly(6, 30),
            Bloecke =
            {
                new BlockEingabe { Typ = VeranstaltungBlockTyp.MarkdownText, MarkdownInhalt = "**Programm**\nZeile 2" },
                new BlockEingabe { Typ = VeranstaltungBlockTyp.BilderGalerie, BildIds = { 4, 2 }, BilderProReihe = 2 }
            },
            Tage = { new TagEingabe { Id = 3, Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = null, MaxTeilnehmer = 20 } }
        };

        await SichernAsync(Olga, Schluessel, eingabe);
        var geladen = (await Speicher.LadenAsync<VeranstaltungEingabe>(Olga, Schluessel, Abbruch)).ShouldNotBeNull();

        geladen.GeaendertUtc.ShouldBe(Jetzt);
        EntwurfSpeicher.AlsJson(geladen.Daten).ShouldBe(EntwurfSpeicher.AlsJson(eingabe));
        geladen.Daten.Titel.ShouldBe("Herbstseminar „Kihon“");
        geladen.Daten.RowVersion.ShouldBe(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        geladen.Daten.Bloecke[1].BildIds.ShouldBe([4, 2]);
        geladen.Daten.Tage.ShouldHaveSingleItem().Ende.ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Je_Benutzer_und_Schluessel_gibt_es_einen_Entwurf_der_ueberschrieben_wird()
    {
        await SichernAsync(Olga, Schluessel, new VeranstaltungEingabe { Titel = "Erster Stand" });
        _zeit.Advance(TimeSpan.FromMinutes(5));
        await SichernAsync(Olga, Schluessel, new VeranstaltungEingabe { Titel = "Zweiter Stand" });
        await SichernAsync(Olga, "veranstaltung:7", new VeranstaltungEingabe { Titel = "Andere Seite" });
        await SichernAsync(Paul, Schluessel, new VeranstaltungEingabe { Titel = "Von Paul" });

        (await EntwuerfeAsync()).Count.ShouldBe(3);
        var olgas = (await Speicher.LadenAsync<VeranstaltungEingabe>(Olga, Schluessel, Abbruch)).ShouldNotBeNull();
        olgas.Daten.Titel.ShouldBe("Zweiter Stand");
        olgas.GeaendertUtc.ShouldBe(Jetzt.AddMinutes(5));
        (await Speicher.LadenAsync<VeranstaltungEingabe>(Paul, Schluessel, Abbruch))!.Daten.Titel.ShouldBe("Von Paul");
        (await Speicher.LadenAsync<VeranstaltungEingabe>(Paul, "veranstaltung:7", Abbruch)).ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Loeschen_entfernt_nur_den_einen_Entwurf()
    {
        await SichernAsync(Olga, Schluessel, new VeranstaltungEingabe { Titel = "Neu" });
        await SichernAsync(Olga, "veranstaltung:7", new VeranstaltungEingabe { Titel = "Sieben" });

        await Speicher.LoeschenAsync(Olga, Schluessel, Abbruch);

        (await EntwuerfeAsync()).ShouldHaveSingleItem().Schluessel.ShouldBe("veranstaltung:7");
    }

    [DatenbankFact]
    public async Task Abgelaufene_Entwuerfe_werden_nicht_geladen_und_beim_naechsten_neuen_Entwurf_entfernt()
    {
        await SichernAsync(Olga, Schluessel, new VeranstaltungEingabe { Titel = "Alt" });

        _zeit.Advance(EntwurfSpeicher.AufbewahrenFuer - TimeSpan.FromMinutes(1));
        (await Speicher.LadenAsync<VeranstaltungEingabe>(Olga, Schluessel, Abbruch)).ShouldNotBeNull();

        _zeit.Advance(TimeSpan.FromMinutes(2));
        (await Speicher.LadenAsync<VeranstaltungEingabe>(Olga, Schluessel, Abbruch)).ShouldBeNull();

        await SichernAsync(Paul, Schluessel, new VeranstaltungEingabe { Titel = "Frisch" });
        (await EntwuerfeAsync()).ShouldHaveSingleItem().BenutzerId.ShouldBe(Paul);
    }

    [DatenbankFact]
    public async Task Entwurf_der_nicht_mehr_zum_Formular_passt_wird_ignoriert()
    {
        await Speicher.SpeichernAsync(Olga, Schluessel, "{ das ist kein JSON", [], Abbruch);

        (await Speicher.LadenAsync<VeranstaltungEingabe>(Olga, Schluessel, Abbruch)).ShouldBeNull();
    }

    [DatenbankFact]
    public async Task Sicherung_haelt_vorlaeufige_Bilder_des_Entwurfs_am_Leben()
    {
        var frueher = Jetzt.AddDays(-1);
        int imEntwurf, gespeichert, fremd;
        await using (var kontext = Datenbank.NeuerKontext())
        {
            var bilder = new[] { Bild("im entwurf", frueher), Bild("laengst gespeichert", null), Bild("fremd", frueher) };
            kontext.Images.AddRange(bilder);
            await kontext.SaveChangesAsync(Abbruch);
            (imEntwurf, gespeichert, fremd) = (bilder[0].Id, bilder[1].Id, bilder[2].Id);
        }

        await SichernAsync(Olga, Schluessel, new VeranstaltungEingabe { Titel = "Mit Bildern" }, imEntwurf, gespeichert);

        await using (var kontext = Datenbank.NeuerKontext())
        {
            var vorlaeufig = await kontext.Images.ToDictionaryAsync(i => i.Id, i => i.VorlaeufigSeitUtc, Abbruch);
            vorlaeufig[imEntwurf].ShouldBe(Jetzt, "die Frist des BildAufraeumJob beginnt neu");
            vorlaeufig[gespeichert].ShouldBeNull("gespeicherte Bilder werden nicht wieder vorläufig");
            vorlaeufig[fremd].ShouldBe(frueher);
        }

        (await Speicher.FehlendeBilderAsync([imEntwurf, 999_999, fremd], Abbruch)).ShouldBe([999_999]);
        (await Speicher.FehlendeBilderAsync([], Abbruch)).ShouldBeEmpty();
    }

    [DatenbankFact]
    public async Task Mit_dem_Benutzer_verschwinden_seine_Entwuerfe()
    {
        await SichernAsync(Olga, Schluessel, new VeranstaltungEingabe { Titel = "Olgas" });
        await SichernAsync(Paul, Schluessel, new VeranstaltungEingabe { Titel = "Pauls" });

        await using (var kontext = Datenbank.NeuerKontext())
            await kontext.Users.Where(u => u.Id == Olga).ExecuteDeleteAsync(Abbruch);

        (await EntwuerfeAsync()).ShouldHaveSingleItem().BenutzerId.ShouldBe(Paul);
    }

    private static DbImage Bild(string titel, DateTime? vorlaeufigSeit) =>
        new() { Title = titel, ImageData = [1, 2, 3], ContentType = "image/jpeg", CreatedAt = Jetzt.AddDays(-30), VorlaeufigSeitUtc = vorlaeufigSeit };
}
