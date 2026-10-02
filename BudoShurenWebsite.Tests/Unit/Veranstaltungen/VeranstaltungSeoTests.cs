using System.Text.Json;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class VeranstaltungSeoTests
{
    private const string Basis = "https://dojo.example/";

    private static VeranstaltungAnzeige Anzeige(
        VeranstaltungStatus status = VeranstaltungStatus.Veroeffentlicht,
        VeranstaltungSichtbarkeit sichtbarkeit = VeranstaltungSichtbarkeit.Oeffentlich,
        string? ort = "Dojo",
        IReadOnlyList<int>? bilder = null,
        params TagAnzeige[] tage) =>
        new("Herbst <seminar>", "herbstseminar", "Zwei Tage Aikido",
            bilder is null ? [] : [new InhaltsBlockAnzeige(VeranstaltungBlockTyp.BilderGalerie, "", 3, null, bilder)],
            ort, "Ulmer Str. 178, Augsburg", null, null, null, null, status, sichtbarkeit, true, AnmeldeZustand.Offen, null, null,
            Teilnahmemodus.NurGesamt, 1, 0, true, FormularFeldModus.Aus, FormularFeldModus.Aus, FormularFeldModus.Aus, FormularFeldModus.Aus,
            tage.Length > 0 ? tage :
            [
                new(1, new DateOnly(2026, 11, 14), new TimeOnly(10, 0), new TimeOnly(17, 0), null, false, null),
                new(2, new DateOnly(2026, 11, 15), new TimeOnly(9, 0), new TimeOnly(13, 0), null, false, null)
            ]);

    private static JsonElement Daten(VeranstaltungAnzeige v) => JsonDocument.Parse(VeranstaltungSeo.StrukturierteDaten(v, Basis)!).RootElement;

    [Fact]
    public void Event_mit_Zeitraum_in_Ortszeit_Ort_und_Veranstalter()
    {
        var daten = Daten(Anzeige());

        daten.GetProperty("@type").GetString().ShouldBe("Event");
        daten.GetProperty("name").GetString().ShouldBe("Herbst <seminar>");
        daten.GetProperty("url").GetString().ShouldBe("https://dojo.example/veranstaltungen/herbstseminar");
        daten.GetProperty("startDate").GetString().ShouldBe("2026-11-14T10:00:00+01:00");
        daten.GetProperty("endDate").GetString().ShouldBe("2026-11-15T13:00:00+01:00");
        daten.GetProperty("eventStatus").GetString().ShouldBe("https://schema.org/EventScheduled");
        daten.GetProperty("description").GetString().ShouldBe("Zwei Tage Aikido");
        daten.GetProperty("location").GetProperty("address").GetString().ShouldBe("Ulmer Str. 178, Augsburg");
        daten.GetProperty("organizer").GetProperty("name").GetString().ShouldBe(VeranstaltungSeo.Veranstalter);
        daten.TryGetProperty("image", out _).ShouldBeFalse();
    }

    [Fact]
    public void Sommerzeit_offenes_Ende_Absage_und_Bilder()
    {
        var daten = Daten(Anzeige(VeranstaltungStatus.Abgesagt, bilder: [7, 9],
            tage: new TagAnzeige(1, new DateOnly(2026, 7, 4), new TimeOnly(19, 0), null, "Sommerfest", false, null)));

        daten.GetProperty("startDate").GetString().ShouldBe("2026-07-04T19:00:00+02:00");
        daten.TryGetProperty("endDate", out _).ShouldBeFalse("offenes Ende");
        daten.GetProperty("eventStatus").GetString().ShouldBe("https://schema.org/EventCancelled");
        daten.GetProperty("image").EnumerateArray().Select(b => b.GetString())
            .ShouldBe(["https://dojo.example/Account/Member/Filesave/GetImage/7", "https://dojo.example/Account/Member/Filesave/GetImage/9"]);
    }

    [Fact]
    public void Kann_gefahrlos_in_ein_script_Element()
    {
        VeranstaltungSeo.StrukturierteDaten(Anzeige(), Basis)!.ShouldNotContain("<");
    }

    [Fact]
    public void Nur_per_Link_wird_nicht_indexiert()
    {
        VeranstaltungSeo.Indexieren(Anzeige()).ShouldBeTrue();
        VeranstaltungSeo.Indexieren(Anzeige(sichtbarkeit: VeranstaltungSichtbarkeit.NurPerLink)).ShouldBeFalse();
    }
}
