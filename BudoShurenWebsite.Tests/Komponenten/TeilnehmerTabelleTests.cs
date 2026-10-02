using Bunit;
using BudoShurenWebsite.Components.Shared.Veranstaltungen;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Komponenten;

[Trait("Category", "Komponente")]
public class TeilnehmerTabelleTests : BunitContext
{
    private static TeilnehmerZeile Zeile(int id, string nachname, AnmeldungStatus status = AnmeldungStatus.Angemeldet, bool neu = false, params int[] tage) =>
        new(id, "Max", nachname, $"{nachname.ToLowerInvariant()}@example.org", status, 1, tage, null, null,
            AnmeldungQuelle.Formular, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), neu, false);

    private static readonly TagBelegung[] Tage =
    [
        new(1, new DateOnly(2026, 11, 14), new TimeOnly(10, 0), new TimeOnly(16, 0), null, false, 20, 2),
        new(2, new DateOnly(2026, 11, 15), new TimeOnly(10, 0), new TimeOnly(16, 0), null, true, 20, 0),
        new(3, new DateOnly(2026, 11, 16), new TimeOnly(10, 0), new TimeOnly(16, 0), null, false, 20, 2)
    ];

    private static readonly TeilnehmerZeile[] Standard =
    [
        Zeile(1, "Zander"),
        Zeile(2, "Adler", neu: true),
        Zeile(3, "Berg", AnmeldungStatus.Storniert, neu: true)
    ];

    private IRenderedComponent<TeilnehmerTabelle> Tabelle(IReadOnlyList<TeilnehmerZeile> teilnehmer,
        Teilnahmemodus modus = Teilnahmemodus.NurGesamt,
        Action<TeilnehmerFilterArt>? filterGeaendert = null,
        Action<int>? oeffnen = null,
        Action<IReadOnlyCollection<int>>? gesehen = null) =>
        Render<TeilnehmerTabelle>(p => p
            .Add(x => x.Teilnehmer, teilnehmer)
            .Add(x => x.Tage, Tage)
            .Add(x => x.Teilnahmemodus, modus)
            .Add(x => x.Filter, TeilnehmerFilterArt.Aktiv)
            .Add(x => x.FilterChanged, filterGeaendert ?? (_ => { }))
            .Add(x => x.OnOeffnen, oeffnen ?? (_ => { }))
            .Add(x => x.OnAuswahlGesehen, gesehen ?? (_ => { })));

    private static int[] Zeilen(IRenderedComponent<TeilnehmerTabelle> tabelle) =>
        tabelle.FindAll("tr[data-anmeldung]").Select(z => int.Parse(z.GetAttribute("data-anmeldung")!)).ToArray();

    [Fact]
    public void Zeigt_aktive_Anmeldungen_sortiert_und_zaehlt_pro_Filter()
    {
        var tabelle = Tabelle(Standard);

        Zeilen(tabelle).ShouldBe([2, 1]);
        tabelle.Find("[data-filter=Aktiv]").TextContent.ShouldContain("(2)");
        tabelle.Find("[data-filter=Neu]").TextContent.ShouldContain("(2)");
        tabelle.Find("[data-filter=Abgemeldet]").TextContent.ShouldContain("(1)");
        tabelle.Find("tr[data-anmeldung=\"2\"]").TextContent.ShouldContain("neu");
    }

    [Fact]
    public void Filterwechsel_meldet_den_Filter_und_zeigt_passende_Zeilen()
    {
        TeilnehmerFilterArt? gemeldet = null;
        var tabelle = Tabelle(Standard, filterGeaendert: f => gemeldet = f);

        tabelle.Find("[data-filter=Neu]").Click();

        gemeldet.ShouldBe(TeilnehmerFilterArt.Neu);
        Zeilen(tabelle).ShouldBe([2, 3]);
    }

    [Fact]
    public void Suche_grenzt_ein_und_meldet_leere_Treffer()
    {
        var tabelle = Tabelle(Standard);

        tabelle.Find("input[type=search]").Input("zan");
        Zeilen(tabelle).ShouldBe([1]);

        tabelle.Find("input[type=search]").Input("niemand");
        tabelle.Markup.ShouldContain("Keine Anmeldungen für diesen Filter.");
    }

    [Fact]
    public void Tagesmatrix_nur_bei_Teilanmeldung_und_ohne_abgesagte_Tage()
    {
        Tabelle(Standard).Markup.ShouldNotContain("14.11.");

        var tabelle = Tabelle([Zeile(1, "Adler", tage: [3])], Teilnahmemodus.EinzelneTage);

        tabelle.FindAll("th").Select(th => th.TextContent.Trim()).ShouldBe(["", "Name", "Status", "Pers.", "Sa 14.11.", "Mo 16.11.", "Verein", "Angemeldet"]);
        tabelle.FindAll("tr[data-anmeldung] td").Select(td => td.TextContent.Trim()).Skip(4).Take(2).ShouldBe(["", "✓"]);
    }

    [Fact]
    public void Zeilenklick_oeffnet_die_Anmeldung()
    {
        int? geoeffnet = null;
        var tabelle = Tabelle(Standard, oeffnen: id => geoeffnet = id);

        tabelle.Find("tr[data-anmeldung=\"1\"]").Click();

        geoeffnet.ShouldBe(1);
    }

    [Fact]
    public void Auswahl_als_gesehen_markieren_meldet_die_Ids_und_leert_die_Auswahl()
    {
        IReadOnlyCollection<int>? gemeldet = null;
        var tabelle = Tabelle(Standard, gesehen: ids => gemeldet = ids);

        tabelle.Find("thead input[type=checkbox]").Change(true);
        tabelle.Markup.ShouldContain("2 ausgewählt");
        tabelle.Find("tr[data-anmeldung=\"1\"] input[type=checkbox]").Change(false);
        tabelle.Markup.ShouldContain("1 ausgewählt");

        tabelle.FindAll("button").Single(b => b.TextContent.Trim() == "Als gesehen markieren").Click();

        gemeldet.ShouldBe([2]);
        tabelle.Markup.ShouldNotContain("ausgewählt");
    }

    [Fact]
    public void Nach_dem_Neuladen_bleiben_nur_vorhandene_Anmeldungen_ausgewaehlt()
    {
        var tabelle = Tabelle(Standard);
        tabelle.Find("thead input[type=checkbox]").Change(true);

        tabelle.Render(p => p.Add(x => x.Teilnehmer, [Zeile(1, "Zander")]));

        tabelle.Markup.ShouldContain("1 ausgewählt");
    }
}
