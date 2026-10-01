using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class VeranstaltungAenderungsRegelnTests
{
    private static readonly DateOnly Samstag = new(2026, 11, 14);

    private static VeranstaltungAenderung Aenderung(
        Teilnahmemodus neuerModus = Teilnahmemodus.NurGesamt,
        string neuerSlug = "seminar",
        bool warVeroeffentlicht = false,
        int aktiveAnmeldungen = 0,
        params TagAenderung[] tage) =>
        new(Teilnahmemodus.NurGesamt, neuerModus, "seminar", neuerSlug, warVeroeffentlicht, aktiveAnmeldungen, tage);

    [Fact]
    public void Ohne_Anmeldungen_ist_alles_erlaubt()
    {
        var aenderung = Aenderung(Teilnahmemodus.EinzelneTage, "neuer-slug", warVeroeffentlicht: false, aktiveAnmeldungen: 0,
            new TagAenderung(1, Samstag, Entfernen: true, NeuesMax: null, Belegt: 0, AktiveAnmeldungen: 0));

        VeranstaltungAenderungsRegeln.Pruefen(aenderung).ShouldBeEmpty();
    }

    [Fact]
    public void Teilnahmemodus_ist_mit_Anmeldungen_gesperrt()
    {
        var fehler = VeranstaltungAenderungsRegeln.Pruefen(Aenderung(Teilnahmemodus.EinzelneTage, aktiveAnmeldungen: 1));

        fehler.ShouldHaveSingleItem().ShouldContain("Teilnahmemodus");
    }

    [Fact]
    public void Slug_ist_nach_Veroeffentlichung_gesperrt()
    {
        VeranstaltungAenderungsRegeln.Pruefen(Aenderung(neuerSlug: "anders", warVeroeffentlicht: true))
            .ShouldHaveSingleItem().ShouldContain("Slug");
        VeranstaltungAenderungsRegeln.Pruefen(Aenderung(neuerSlug: "seminar", warVeroeffentlicht: true)).ShouldBeEmpty();
    }

    [Fact]
    public void Tag_mit_Anmeldungen_kann_nur_abgesagt_werden()
    {
        var fehler = VeranstaltungAenderungsRegeln.Pruefen(Aenderung(tage:
            new TagAenderung(1, Samstag, Entfernen: true, NeuesMax: null, Belegt: 3, AktiveAnmeldungen: 2)));

        fehler.ShouldHaveSingleItem().ShouldContain("14.11.2026");
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)]
    [InlineData(null, 50, false)]
    public void Kapazitaet_nicht_unter_die_Belegung(int? neuesMax, int belegt, bool fehlerErwartet)
    {
        var fehler = VeranstaltungAenderungsRegeln.Pruefen(Aenderung(tage:
            new TagAenderung(1, Samstag, Entfernen: false, NeuesMax: neuesMax, Belegt: belegt, AktiveAnmeldungen: 1)));

        (fehler.Count == 1).ShouldBe(fehlerErwartet);
    }
}
