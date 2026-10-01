using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

/// <summary>Erkennung einer Termin- oder Ortsänderung beim Speichern (Plan 6.2).</summary>
[Trait("Category", "Unit")]
public class VeranstaltungEingabeTests
{
    private static VeranstaltungEingabe Eingabe(string? ort = "Halle", params TagEingabe[] tage) =>
        new() { Titel = "Herbstseminar", Ort = ort, Adresse = "Hauptstr. 1", Tage = tage.Length > 0 ? [.. tage] : [Tag(14), Tag(15)] };

    private static TagEingabe Tag(int tagImMonat, int beginn = 10, bool abgesagt = false, string? titel = null, int? max = null) =>
        new() { Datum = new DateOnly(2026, 11, tagImMonat), Beginn = new TimeOnly(beginn, 0), Ende = new TimeOnly(16, 0), Abgesagt = abgesagt, Titel = titel, MaxTeilnehmer = max };

    [Fact]
    public void Gleich_bei_anderer_Reihenfolge_Leerzeichen_Titel_und_Kapazitaet()
    {
        var vorher = Eingabe();
        var nachher = Eingabe(" Halle ", Tag(15, titel: "Kumite", max: 30), Tag(14));
        nachher.Titel = "Neuer Titel";

        nachher.TerminUndOrt().ShouldBe(vorher.TerminUndOrt());
    }

    [Fact]
    public void Abgesagte_Tage_zaehlen_nicht() =>
        Eingabe("Halle", Tag(14), Tag(15), Tag(16, abgesagt: true)).TerminUndOrt().ShouldBe(Eingabe().TerminUndOrt());

    [Fact]
    public void Ungleich_bei_Ort_Uhrzeit_oder_Datum()
    {
        var basis = Eingabe().TerminUndOrt();

        Eingabe("Dojo").TerminUndOrt().ShouldNotBe(basis);
        Eingabe("Halle", Tag(14, beginn: 9), Tag(15)).TerminUndOrt().ShouldNotBe(basis);
        Eingabe("Halle", Tag(14), Tag(16)).TerminUndOrt().ShouldNotBe(basis);
        Eingabe("Halle", Tag(14)).TerminUndOrt().ShouldNotBe(basis);
    }
}
