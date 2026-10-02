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
        ["Sa 14.11."], ["a@example.org"]);

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
            Tage = ["Sa 14.11.", "So 15.11."],
            InfoEmails = ["a@example.org", "b@example.org"]
        };

        var aenderungen = EreignisDiff.Erstellen(Basis, neu);

        aenderungen.Select(a => a.Art).ShouldBe(
            [AnmeldungEreignisArt.BegleitungGeaendert, AnmeldungEreignisArt.TageGeaendert, AnmeldungEreignisArt.InfoEmailsGeaendert]);
        aenderungen.Single(a => a.Art == AnmeldungEreignisArt.TageGeaendert).DetailsJson
            .ShouldBe("{\"Alt\":[\"Sa 14.11.\"],\"Neu\":[\"Sa 14.11.\",\"So 15.11.\"]}");
        aenderungen.Single(a => a.Art == AnmeldungEreignisArt.BegleitungGeaendert).DetailsJson
            .ShouldBe("{\"Alt\":\"1\",\"Neu\":\"2\"}");
    }

    [Fact]
    public void Umlaute_bleiben_lesbar()
    {
        EreignisDiff.Wert("müller@example.org", "möller@example.org").ShouldBe("{\"Alt\":\"müller@example.org\",\"Neu\":\"möller@example.org\"}");
    }

    [Fact]
    public void Stand_aus_Anmeldung_rechnet_Tag_Ids_in_Kurztexte_um()
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

        stand.Tage.ShouldBe(["Sa 14.11.", "So 15.11."]);
        stand.InfoEmails.ShouldBe(["a@example.org", "b@example.org"]);
    }

    [Fact]
    public void Wechsel_zwischen_Terminen_am_selben_Datum_wird_erkannt()
    {
        VeranstaltungsTag[] tage =
        [
            new() { Id = 1, Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Titel = "Training" },
            new() { Id = 2, Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(19, 0), Titel = "Essen" }
        ];
        Anmeldung Mit(int tagId) => new() { Tage = { new AnmeldungTag { VeranstaltungsTagId = tagId } } };

        var aenderung = EreignisDiff.Erstellen(AnmeldungStand.Von(Mit(1), tage), AnmeldungStand.Von(Mit(2), tage)).ShouldHaveSingleItem();

        aenderung.Art.ShouldBe(AnmeldungEreignisArt.TageGeaendert);
        EreignisText.Beschreiben(aenderung.Art, aenderung.DetailsJson).ShouldBe("Termine: Sa 14.11. 10:00 Training → Sa 14.11. 19:00 Essen");
    }
}
