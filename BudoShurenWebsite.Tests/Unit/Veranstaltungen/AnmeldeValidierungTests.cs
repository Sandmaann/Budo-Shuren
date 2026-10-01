using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class AnmeldeValidierungTests
{
    private static readonly VeranstaltungsTag[] Tage =
    [
        new() { Id = 1, Datum = new DateOnly(2026, 11, 14) },
        new() { Id = 2, Datum = new DateOnly(2026, 11, 15) },
        new() { Id = 3, Datum = new DateOnly(2026, 11, 16), Abgesagt = true }
    ];

    private static Veranstaltung Veranstaltung(Teilnahmemodus modus = Teilnahmemodus.NurGesamt) => new()
    {
        Teilnahmemodus = modus,
        MaxBegleitpersonen = 3,
        TelefonFeld = FormularFeldModus.Pflicht,
        VereinFeld = FormularFeldModus.Optional,
        GraduierungFeld = FormularFeldModus.Aus,
        BemerkungFeld = FormularFeldModus.Optional
    };

    private static AnmeldeEingabe Gueltig() => new()
    {
        Vorname = " Max ",
        Nachname = "Muster",
        Email = " Max@Example.ORG ",
        Telefon = "0821 123",
        DatenschutzAkzeptiert = true
    };

    private static AnmeldePruefung Pruefen(AnmeldeEingabe eingabe, Veranstaltung? veranstaltung = null) =>
        AnmeldeValidierung.Pruefen(veranstaltung ?? Veranstaltung(), Tage, eingabe, maxInfoEmails: 5);

    [Fact]
    public void Gueltige_Eingabe_wird_bereinigt()
    {
        var eingabe = Gueltig();
        eingabe.Graduierung = "3. Dan"; // Feld ist ausgeschaltet
        eingabe.Verein = "  ";

        var ergebnis = Pruefen(eingabe);

        ergebnis.Fehler.ShouldBeEmpty();
        var a = ergebnis.Anmeldung.ShouldNotBeNull();
        a.Vorname.ShouldBe("Max");
        a.Email.ShouldBe("max@example.org");
        a.Graduierung.ShouldBeNull("ausgeschaltete Felder werden ignoriert");
        a.Verein.ShouldBeNull();
        a.TagIds.ShouldBeEmpty("bei NurGesamt werden keine Tage gespeichert");
    }

    [Fact]
    public void Pflichtfelder_und_Datenschutz()
    {
        var ergebnis = Pruefen(new AnmeldeEingabe());

        ergebnis.IstGueltig.ShouldBeFalse();
        ergebnis.Fehler.Keys.ShouldBe(
            [nameof(AnmeldeEingabe.Vorname), nameof(AnmeldeEingabe.Nachname), nameof(AnmeldeEingabe.Email), nameof(AnmeldeEingabe.Telefon), nameof(AnmeldeEingabe.DatenschutzAkzeptiert)],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData("keine-adresse")]
    [InlineData("Max <max@example.org>")]
    public void Ungueltige_Adresse(string email)
    {
        var eingabe = Gueltig();
        eingabe.Email = email;

        Pruefen(eingabe).Fehler.Keys.ShouldBe([nameof(AnmeldeEingabe.Email)]);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(4, true)]
    public void Begleitpersonen_im_erlaubten_Bereich(int anzahl, bool fehler)
    {
        var eingabe = Gueltig();
        eingabe.AnzahlBegleitpersonen = anzahl;

        Pruefen(eingabe).Fehler.ContainsKey(nameof(AnmeldeEingabe.AnzahlBegleitpersonen)).ShouldBe(fehler);
    }

    [Fact]
    public void Einzelne_Tage_nur_angebotene_und_mindestens_die_Mindestzahl()
    {
        var veranstaltung = Veranstaltung(Teilnahmemodus.EinzelneTage);
        veranstaltung.MinTageBeiTeilanmeldung = 2;

        var eingabe = Gueltig();
        eingabe.TagIds = [1];
        Pruefen(eingabe, veranstaltung).Fehler[nameof(AnmeldeEingabe.TagIds)].ShouldContain("mindestens 2");

        eingabe.TagIds = [1, 3]; // Tag 3 ist abgesagt
        Pruefen(eingabe, veranstaltung).Fehler[nameof(AnmeldeEingabe.TagIds)].ShouldContain("angebotene");

        eingabe.TagIds = [2, 1, 1];
        Pruefen(eingabe, veranstaltung).Anmeldung!.TagIds.ShouldBe([1, 2]);
    }

    [Fact]
    public void Fehlende_Tagesliste_aus_dem_Formular_ist_ein_Feldfehler_keine_Exception()
    {
        var eingabe = Gueltig();
        eingabe.TagIds = null!;

        Pruefen(eingabe, Veranstaltung(Teilnahmemodus.EinzelneTage)).Fehler.ShouldContainKey(nameof(AnmeldeEingabe.TagIds));
    }

    [Fact]
    public void Info_Adressen_nur_fuer_Begleitpersonen_und_ohne_eigene_Adresse()
    {
        var eingabe = Gueltig();
        eingabe.InfoEmails = "a@example.org";
        Pruefen(eingabe).Fehler[nameof(AnmeldeEingabe.InfoEmails)].ShouldContain("Begleitpersonen");

        eingabe.AnzahlBegleitpersonen = 2;
        eingabe.InfoEmails = "A@Example.org\n a@example.org ; max@example.org, b@example.org";
        var ergebnis = Pruefen(eingabe);
        ergebnis.Fehler.ShouldBeEmpty();
        ergebnis.Anmeldung!.InfoEmails.ShouldBe(["a@example.org", "b@example.org"]);

        eingabe.InfoEmails = "a@example.org, b@example.org, c@example.org";
        Pruefen(eingabe).Fehler[nameof(AnmeldeEingabe.InfoEmails)].ShouldContain("Höchstens 2");

        eingabe.InfoEmails = "a@example.org, kaputt";
        Pruefen(eingabe).Fehler[nameof(AnmeldeEingabe.InfoEmails)].ShouldContain("kaputt");
    }

    [Fact]
    public void Info_Adressen_hoechstens_die_Obergrenze()
    {
        var veranstaltung = Veranstaltung();
        veranstaltung.MaxBegleitpersonen = 10;
        var eingabe = Gueltig();
        eingabe.AnzahlBegleitpersonen = 10;
        eingabe.InfoEmails = string.Join("\n", Enumerable.Range(1, 6).Select(i => $"p{i}@example.org"));

        Pruefen(eingabe, veranstaltung).Fehler[nameof(AnmeldeEingabe.InfoEmails)].ShouldContain("Höchstens 5");
    }
}
