using Bunit;
using BudoShurenWebsite.Components.Pages.Veranstaltungen;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.AspNetCore.Components.Forms;

namespace BudoShurenWebsite.Tests.Komponenten;

[Trait("Category", "Komponente")]
public class AnmeldeFelderTests : BunitContext
{
    // Samstag zwei Termine (das Essen ist ausgebucht), Sonntag einer
    private static readonly TagAnzeige[] Termine =
    [
        new(3, new DateOnly(2026, 11, 15), new TimeOnly(10, 0), new TimeOnly(13, 0), null, false, null),
        new(2, new DateOnly(2026, 11, 14), new TimeOnly(19, 0), null, "Essen", false, 0),
        new(1, new DateOnly(2026, 11, 14), new TimeOnly(10, 0), new TimeOnly(17, 0), "Training", false, 10)
    ];

    private IRenderedComponent<AnmeldeFelder> Felder(IReadOnlyList<TagAnzeige> termine, Teilnahmemodus modus = Teilnahmemodus.EinzelneTage)
    {
        var eingabe = new AnmeldeEingabe();
        return Render<AnmeldeFelder>(p => p
            .AddCascadingValue(new EditContext(eingabe))
            .Add(x => x.Praefix, "Formular")
            .Add(x => x.Eingabe, eingabe)
            .Add(x => x.Tage, VeranstaltungTexte.TagAuswahl(termine, []))
            .Add(x => x.TagInfos, termine)
            .Add(x => x.Einstellungen, new AnmeldeFormularEinstellungen(modus, 1, 0,
                FormularFeldModus.Aus, FormularFeldModus.Aus, FormularFeldModus.Aus, FormularFeldModus.Aus)));
    }

    private static string Text(AngleSharp.Dom.IElement element) => string.Join(" ", element.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public void Termine_nach_Datum_gruppiert_ausgebuchte_nicht_waehlbar()
    {
        var felder = Felder(Termine);

        felder.FindAll("fieldset p").Select(Text).ShouldBe(["Samstag, 14. November 2026", "Sonntag, 15. November 2026"]);
        felder.FindAll("fieldset label").Select(Text).ShouldBe(
            ["10:00 – 17:00 Uhr · Training", "ab 19:00 Uhr · Essen – ausgebucht", "10:00 – 13:00 Uhr"]);
        felder.FindAll("fieldset input[type=checkbox]").Select(c => c.HasAttribute("disabled")).ShouldBe([false, true, false]);
    }

    [Fact]
    public void Formularnamen_behalten_den_Index_der_Auswahl()
    {
        var felder = Felder(Termine);

        // Tage[i] gehört zu Termine[i] (TagAuswahl), auch wenn die Anzeige anders sortiert ist
        var training = felder.Find("input[type=hidden][value='1']");
        training.GetAttribute("name").ShouldBe("Formular.Tage[2].Id");
        training.NextElementSibling!.QuerySelector("input[type=checkbox]")!.GetAttribute("name").ShouldBe("Formular.Tage[2].Gewaehlt");
    }

    [Fact]
    public void Alle_auswaehlen_nur_bei_mehreren_waehlbaren_Terminen()
    {
        Felder(Termine).Find("#formular-alle-termine").GetAttribute("onclick").ShouldContain("dispatchEvent(new Event('change'");
        Felder([Termine[1], Termine[2]]).FindAll("#formular-alle-termine").ShouldBeEmpty("nur ein Termin ist wählbar");
    }

    [Fact]
    public void Hinweis_auf_Pflichtfelder_und_ohne_Terminauswahl_bei_Gesamtanmeldung()
    {
        var felder = Felder(Termine, Teilnahmemodus.NurGesamt);

        felder.Markup.ShouldContain("Felder mit * sind Pflichtfelder.");
        felder.FindAll("fieldset").ShouldBeEmpty();
    }
}
