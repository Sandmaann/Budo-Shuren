using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class EreignisTextTests
{
    private static AnmeldungStand Stand(string vorname = "Max", string? verein = null, int begleitung = 0, DateOnly[]? tage = null, string[]? info = null) =>
        new(vorname, "Muster", null, verein, null, null, begleitung, tage ?? [], info ?? []);

    [Fact]
    public void Datenaenderung_mit_alt_und_neu()
    {
        var aenderung = EreignisDiff.Erstellen(Stand(), Stand("Moritz", verein: "Dojo Nord")).Single();

        EreignisText.Beschreiben(aenderung.Art, aenderung.DetailsJson).ShouldBe("Vorname: Max → Moritz; Verein: – → Dojo Nord");
    }

    [Fact]
    public void Begleitung_Tage_und_Info_Adressen()
    {
        var aenderungen = EreignisDiff.Erstellen(
            Stand(),
            Stand(begleitung: 2, tage: [new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)], info: ["a@example.org"]));

        aenderungen.Select(a => EreignisText.Beschreiben(a.Art, a.DetailsJson)).ShouldBe(
        [
            "Begleitpersonen: 0 → 2",
            "Tage: – → 14.11.2026, 15.11.2026",
            "Info-Adressen: – → a@example.org"
        ]);
    }

    [Fact]
    public void E_Mail_Wechsel_Ablehnung_und_Info_Abmeldung()
    {
        EreignisText.Beschreiben(AnmeldungEreignisArt.EmailGeaendert, EreignisDiff.Wert("a@example.org", "b@example.org"))
            .ShouldBe("E-Mail: a@example.org → b@example.org");
        EreignisText.Beschreiben(AnmeldungEreignisArt.Abgelehnt, EreignisDiff.Grund("Kurs ist nur für Mitglieder"))
            .ShouldBe("Abgelehnt: Kurs ist nur für Mitglieder");
        EreignisText.Beschreiben(AnmeldungEreignisArt.InfoEmailsGeaendert, EreignisDiff.Wert("b@example.org", null))
            .ShouldBe("b@example.org möchte keine Infos mehr");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("{\"Grund\":null}")]
    public void Ohne_brauchbare_Details_der_Name_der_Art(string? details)
    {
        EreignisText.Beschreiben(AnmeldungEreignisArt.Abgelehnt, details).ShouldBe("Abgelehnt");
    }
}
