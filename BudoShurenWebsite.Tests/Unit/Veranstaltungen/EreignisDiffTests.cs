using System.Text.Json;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class EreignisDiffTests
{
    private static readonly AnmeldungStand Basis = new(
        "Max", "Müller", null, "Dojo Nord", null, null, 1,
        [new DateOnly(2026, 11, 14)], ["a@example.org"]);

    [Fact]
    public void Ohne_Aenderung_keine_Ereignisse()
    {
        EreignisDiff.Erstellen(Basis, Basis with { }).ShouldBeEmpty();
        EreignisDiff.Erstellen(Basis, Basis with { Telefon = "" }).ShouldBeEmpty("leer und null sind gleich");
    }

    [Fact]
    public void Persoenliche_Daten_in_einem_Ereignis_mit_alt_und_neu()
    {
        var aenderung = EreignisDiff.Erstellen(Basis, Basis with { Vorname = "Moritz", Verein = null }).ShouldHaveSingleItem();

        aenderung.Art.ShouldBe(AnmeldungEreignisArt.DatenGeaendert);
        var json = JsonDocument.Parse(aenderung.DetailsJson).RootElement;
        json.GetProperty("Vorname").GetProperty("Alt").GetString().ShouldBe("Max");
        json.GetProperty("Vorname").GetProperty("Neu").GetString().ShouldBe("Moritz");
        json.GetProperty("Verein").GetProperty("Neu").ValueKind.ShouldBe(JsonValueKind.Null);
        json.TryGetProperty("Nachname", out _).ShouldBeFalse("unveränderte Felder stehen nicht drin");
    }

    [Fact]
    public void Begleitung_Tage_und_Info_Adressen_jeweils_eigene_Ereignisse()
    {
        var neu = Basis with
        {
            AnzahlBegleitpersonen = 2,
            Tage = [new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)],
            InfoEmails = ["a@example.org", "b@example.org"]
        };

        var aenderungen = EreignisDiff.Erstellen(Basis, neu);

        aenderungen.Select(a => a.Art).ShouldBe(
            [AnmeldungEreignisArt.BegleitungGeaendert, AnmeldungEreignisArt.TageGeaendert, AnmeldungEreignisArt.InfoEmailsGeaendert]);
        aenderungen.Single(a => a.Art == AnmeldungEreignisArt.TageGeaendert).DetailsJson
            .ShouldBe("{\"Alt\":[\"14.11.2026\"],\"Neu\":[\"14.11.2026\",\"15.11.2026\"]}");
        aenderungen.Single(a => a.Art == AnmeldungEreignisArt.BegleitungGeaendert).DetailsJson
            .ShouldBe("{\"Alt\":\"1\",\"Neu\":\"2\"}");
    }

    [Fact]
    public void Umlaute_bleiben_lesbar()
    {
        EreignisDiff.Wert("müller@example.org", "möller@example.org").ShouldBe("{\"Alt\":\"müller@example.org\",\"Neu\":\"möller@example.org\"}");
    }

    [Fact]
    public void Stand_aus_Anmeldung_rechnet_Tag_Ids_in_Daten_um()
    {
        VeranstaltungsTag[] tage =
        [
            new() { Id = 7, Datum = new DateOnly(2026, 11, 15) },
            new() { Id = 3, Datum = new DateOnly(2026, 11, 14) }
        ];
        var anmeldung = new Anmeldung
        {
            Vorname = "Max",
            Nachname = "Muster",
            Tage = { new AnmeldungTag { VeranstaltungsTagId = 7 }, new AnmeldungTag { VeranstaltungsTagId = 3 } },
            InfoEmails = { new AnmeldungInfoEmail { Email = "b@example.org" }, new AnmeldungInfoEmail { Email = "a@example.org" } }
        };

        var stand = AnmeldungStand.Von(anmeldung, tage);

        stand.Tage.ShouldBe([new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)]);
        stand.InfoEmails.ShouldBe(["a@example.org", "b@example.org"]);
    }
}
