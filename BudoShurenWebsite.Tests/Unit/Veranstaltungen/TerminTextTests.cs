using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class TerminTextTests
{
    private static VeranstaltungsTag Termin(int tag, int beginn, int? ende = null, string? titel = null) => new()
    {
        Datum = new DateOnly(2026, 11, tag),
        Beginn = new TimeOnly(beginn, 0),
        Ende = ende is { } e ? new TimeOnly(e, 0) : null,
        Titel = titel
    };

    [Fact]
    public void Datum_ausgeschrieben()
    {
        TerminText.Datum(new DateOnly(2026, 11, 14)).ShouldBe("Samstag, 14. November 2026");
        TerminText.Datum(new DateOnly(2027, 3, 1)).ShouldBe("Montag, 1. März 2027");
    }

    [Fact]
    public void Uhrzeit_mit_und_ohne_Ende()
    {
        TerminText.Uhrzeit(Termin(14, 10, 17)).ShouldBe("10:00 – 17:00 Uhr");
        TerminText.Uhrzeit(Termin(14, 19)).ShouldBe("ab 19:00 Uhr");
    }

    [Fact]
    public void Zeile_mit_Titel()
    {
        TerminText.Zeile(Termin(14, 10, 17)).ShouldBe("Samstag, 14. November 2026, 10:00 – 17:00 Uhr");
        TerminText.Zeile(Termin(14, 19, titel: " Essen ")).ShouldBe("Samstag, 14. November 2026, ab 19:00 Uhr (Essen)");
    }

    [Fact]
    public void Kurz_nur_so_genau_wie_noetig()
    {
        var training = Termin(14, 10, 17, "Training");
        var essen = Termin(14, 19, titel: "Essen");
        var sonntag = Termin(15, 10, 13);
        ITermin[] alle = [training, essen, sonntag];

        TerminText.Kurz(sonntag, alle).ShouldBe("So 15.11.");
        TerminText.Kurz(training, alle).ShouldBe("Sa 14.11. 10:00", "zweiter Termin am selben Datum: mit Uhrzeit");
        TerminText.Kurz(essen, alle, mitTitel: true).ShouldBe("Sa 14.11. 19:00 Essen");
        TerminText.Kurz(sonntag, alle, mitTitel: true).ShouldBe("So 15.11.", "ohne Titel bleibt es beim Datum");
    }

    [Fact]
    public void Kurz_bei_gleicher_Uhrzeit_mit_Titel()
    {
        var kata = Termin(14, 10, 12, "Kata");
        var kumite = Termin(14, 10, 12, "Kumite");

        TerminText.Kurz(kata, [kata, kumite]).ShouldBe("Sa 14.11. 10:00 Kata");
        TerminText.Kurz(kumite, [kata, kumite]).ShouldBe("Sa 14.11. 10:00 Kumite");
    }

    [Theory]
    [InlineData("2026-11-14", "2026-11-14", "14. November 2026")]
    [InlineData("2026-11-14", "2026-11-15", "14.–15. November 2026")]
    [InlineData("2026-10-30", "2026-11-01", "30. Oktober – 1. November 2026")]
    [InlineData("2026-12-30", "2027-01-02", "30. Dezember 2026 – 2. Januar 2027")]
    public void Zeitraum_fasst_Monat_und_Jahr_zusammen(string von, string bis, string erwartet)
    {
        TerminText.Zeitraum([DateOnly.Parse(bis), DateOnly.Parse(von), DateOnly.Parse(von)]).ShouldBe(erwartet);
    }

    [Fact]
    public void Zeitraum_ohne_Daten_ist_leer()
    {
        TerminText.Zeitraum([]).ShouldBeEmpty();
    }

    [Fact]
    public void NachDatum_gruppiert_chronologisch()
    {
        var essen = Termin(14, 19, titel: "Essen");
        var sonntag = Termin(15, 9, 12);
        var training = Termin(14, 10, 17, "Training");

        var gruppen = TerminText.NachDatum([essen, sonntag, training]);

        gruppen.Select(g => g.Key.Day).ShouldBe([14, 15]);
        gruppen[0].ShouldBe([training, essen]);
    }
}
