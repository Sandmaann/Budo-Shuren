using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class EreignisTextTests
{
    private static AnmeldungStand Stand(string vorname = "Max", string? verein = null, int begleitung = 0, string[]? tage = null, string[]? info = null) =>
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
            Stand(begleitung: 2, tage: ["Sa 14.11.", "So 15.11."], info: ["a@example.org"]));

        aenderungen.Select(a => EreignisText.Beschreiben(a.Art, a.DetailsJson)).ShouldBe(
        [
            "Begleitpersonen: 0 → 2",
            "Termine: – → Sa 14.11., So 15.11.",
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

    [Fact]
    public void Fuer_Benachrichtigungen_ohne_Telefon_Bemerkung_und_Adressen()
    {
        var alt = new AnmeldungStand("Max", "Muster", "0170 1", null, null, "alt", 0, [], ["a@example.org"]);
        var neu = new AnmeldungStand("Moritz", "Muster", "0170 2", null, null, "neu", 0, [], ["b@example.org"]);
        var aenderungen = EreignisDiff.Erstellen(alt, neu);

        aenderungen.Select(a => EreignisText.FuerBenachrichtigung(a.Art, a.DetailsJson)).ShouldBe(["Vorname: Max → Moritz", "Info-Adressen geändert"]);
    }

    [Fact]
    public void Fuer_Benachrichtigungen_nur_vertrauliche_Felder_geaendert()
    {
        var aenderung = EreignisDiff.Erstellen(
            new AnmeldungStand("Max", "Muster", "0170 1", null, null, null, 0, [], []),
            new AnmeldungStand("Max", "Muster", "0170 2", null, null, "Komme später", 0, [], [])).Single();

        EreignisText.FuerBenachrichtigung(aenderung.Art, aenderung.DetailsJson).ShouldBe(aenderung.Art.Beschreibung());
        EreignisText.Beschreiben(aenderung.Art, aenderung.DetailsJson).ShouldContain("0170 2", customMessage: "in der Übersicht weiter sichtbar");
    }

    [Fact]
    public void Fuer_Benachrichtigungen_ohne_EMail_und_Ablehnungsgrund()
    {
        EreignisText.FuerBenachrichtigung(AnmeldungEreignisArt.EmailGeaendert, "{\"Alt\":\"a@example.org\",\"Neu\":\"b@example.org\"}")
            .ShouldBe("E-Mail-Adresse geändert");
        EreignisText.FuerBenachrichtigung(AnmeldungEreignisArt.Abgelehnt, "{\"Grund\":\"intern\"}").ShouldBe("Abgelehnt");
    }
}
