using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class VeroeffentlichungsPruefungTests
{
    private static readonly DateTime JetztOrtszeit = new(2026, 10, 1, 12, 0, 0);

    private static Veranstaltung GueltigeVeranstaltung() => new()
    {
        Titel = "Herbstseminar",
        Slug = "herbstseminar",
        KontaktEmail = "seminar@example.org",
        MaxBegleitpersonen = 2
    };

    private static VeranstaltungsTag Tag(int tag, int beginn = 10, int ende = 16, int? max = 20, bool abgesagt = false) => new()
    {
        Datum = new DateOnly(2026, 11, tag),
        Beginn = new TimeOnly(beginn, 0),
        Ende = new TimeOnly(ende, 0),
        MaxTeilnehmer = max,
        Abgesagt = abgesagt
    };

    private static IReadOnlyList<string> Pruefen(Veranstaltung v, params VeranstaltungsTag[] tage) =>
        VeroeffentlichungsPruefung.Pruefen(v, tage, JetztOrtszeit);

    [Fact]
    public void Vollstaendige_Veranstaltung_ist_ok()
    {
        Pruefen(GueltigeVeranstaltung(), Tag(14), Tag(15)).ShouldBeEmpty();
    }

    [Fact]
    public void Ohne_aktive_Tage_nicht_veroeffentlichen()
    {
        Pruefen(GueltigeVeranstaltung()).ShouldHaveSingleItem().ShouldContain("Tag");
        Pruefen(GueltigeVeranstaltung(), Tag(14, abgesagt: true)).ShouldHaveSingleItem().ShouldContain("Tag");
    }

    [Theory]
    [InlineData(null, "fehlt")]
    [InlineData("", "fehlt")]
    [InlineData("keine-adresse", "gültige")]
    [InlineData("Seminar <seminar@example.org>", "gültige")]
    public void Kontaktadresse_ist_Pflicht_und_muss_gueltig_sein(string? adresse, string erwartet)
    {
        var veranstaltung = GueltigeVeranstaltung();
        veranstaltung.KontaktEmail = adresse;

        Pruefen(veranstaltung, Tag(14)).ShouldHaveSingleItem().ShouldContain(erwartet);
    }

    [Fact]
    public void Ende_vor_Beginn_und_Kapazitaet_0_werden_gemeldet()
    {
        var fehler = Pruefen(GueltigeVeranstaltung(), Tag(14, beginn: 16, ende: 10), Tag(15, max: 0));

        fehler.Count.ShouldBe(2);
        fehler.ShouldContain(f => f.Contains("14.11.2026") && f.Contains("Ende"));
        fehler.ShouldContain(f => f.Contains("15.11.2026") && f.Contains("Kapazität"));
    }

    [Fact]
    public void Begonnene_Veranstaltung_nicht_veroeffentlichen()
    {
        var tag = Tag(14);
        tag.Datum = new DateOnly(2026, 10, 1);
        tag.Beginn = new TimeOnly(11, 0); // eine Stunde vor "jetzt"

        Pruefen(GueltigeVeranstaltung(), tag).ShouldHaveSingleItem().ShouldContain("begonnen");
    }

    [Fact]
    public void Beim_Speichern_einer_laufenden_Veranstaltung_zaehlt_der_Beginn_nicht()
    {
        var tag = Tag(14);
        tag.Datum = new DateOnly(2026, 10, 1);
        tag.Beginn = new TimeOnly(11, 0);

        VeroeffentlichungsPruefung.PruefenOhneBeginn(GueltigeVeranstaltung(), [tag]).ShouldBeEmpty();
    }

    [Fact]
    public void Fristen_muessen_vor_dem_Beginn_liegen_und_zueinander_passen()
    {
        var veranstaltung = GueltigeVeranstaltung();
        veranstaltung.AnmeldungAb = new DateTime(2026, 11, 10, 0, 0, 0);
        veranstaltung.AnmeldungBis = new DateTime(2026, 11, 14, 11, 0, 0); // nach Beginn 10:00
        veranstaltung.AenderungenBis = new DateTime(2026, 11, 15, 0, 0, 0);

        var fehler = Pruefen(veranstaltung, Tag(14));

        fehler.ShouldContain(f => f.Contains("Anmeldeschluss"));
        fehler.ShouldContain(f => f.Contains("Änderungsfrist"));
        fehler.ShouldNotContain(f => f.Contains("Anmeldebeginn"));
    }

    [Fact]
    public void Anmeldebeginn_nach_Anmeldeschluss_wird_gemeldet()
    {
        var veranstaltung = GueltigeVeranstaltung();
        veranstaltung.AnmeldungAb = new DateTime(2026, 11, 12, 0, 0, 0);
        veranstaltung.AnmeldungBis = new DateTime(2026, 11, 11, 0, 0, 0);

        Pruefen(veranstaltung, Tag(14)).ShouldHaveSingleItem().ShouldContain("Anmeldebeginn");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void Mindestzahl_Tage_bei_Teilanmeldung(int minTage, bool fehlerErwartet)
    {
        var veranstaltung = GueltigeVeranstaltung();
        veranstaltung.Teilnahmemodus = Teilnahmemodus.EinzelneTage;
        veranstaltung.MinTageBeiTeilanmeldung = minTage;

        (Pruefen(veranstaltung, Tag(14), Tag(15)).Count == 1).ShouldBe(fehlerErwartet);
    }
}
