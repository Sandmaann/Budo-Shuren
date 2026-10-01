using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class AnmeldeFensterTests
{
    // Beginn: Samstag 14.11.2026, 10:00
    private static readonly VeranstaltungsTag[] Tage =
    [
        new() { Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 0) },
        new() { Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0) },
        new() { Datum = new DateOnly(2026, 11, 13), Beginn = new TimeOnly(10, 0), Abgesagt = true }
    ];

    private static Veranstaltung Veroeffentlicht(DateTime? ab = null, DateTime? bis = null) =>
        new() { Status = VeranstaltungStatus.Veroeffentlicht, AnmeldungAb = ab, AnmeldungBis = bis };

    [Fact]
    public void Beginn_ist_der_erste_nicht_abgesagte_Tag()
    {
        AnmeldeFenster.Beginn(Tage).ShouldBe(new DateTime(2026, 11, 14, 10, 0, 0));
        AnmeldeFenster.Beginn([]).ShouldBeNull();
    }

    [Theory]
    [InlineData("2026-10-01 12:00", AnmeldeZustand.Offen)]
    [InlineData("2026-11-14 09:59", AnmeldeZustand.Offen)]
    [InlineData("2026-11-14 10:00", AnmeldeZustand.Geschlossen)] // ohne Anmeldeschluss endet die Anmeldung mit dem Beginn
    public void Ohne_Fristen_bis_zum_Beginn(string jetzt, AnmeldeZustand erwartet)
    {
        AnmeldeFenster.Zustand(Veroeffentlicht(), Tage, DateTime.Parse(jetzt)).ShouldBe(erwartet);
    }

    [Fact]
    public void Mit_Anmeldebeginn_und_Anmeldeschluss()
    {
        var v = Veroeffentlicht(ab: new DateTime(2026, 10, 10), bis: new DateTime(2026, 11, 7, 23, 59, 0));

        AnmeldeFenster.Zustand(v, Tage, new DateTime(2026, 10, 9, 23, 0, 0)).ShouldBe(AnmeldeZustand.NochNichtOffen);
        AnmeldeFenster.Zustand(v, Tage, new DateTime(2026, 10, 10, 0, 0, 0)).ShouldBe(AnmeldeZustand.Offen);
        AnmeldeFenster.Zustand(v, Tage, new DateTime(2026, 11, 8, 0, 0, 0)).ShouldBe(AnmeldeZustand.Geschlossen);
    }

    [Theory]
    [InlineData(VeranstaltungStatus.Entwurf)]
    [InlineData(VeranstaltungStatus.Abgesagt)]
    [InlineData(VeranstaltungStatus.Abgeschlossen)]
    public void Nur_veroeffentlichte_Veranstaltungen_sind_offen(VeranstaltungStatus status)
    {
        var v = Veroeffentlicht();
        v.Status = status;

        AnmeldeFenster.Zustand(v, Tage, new DateTime(2026, 10, 1)).ShouldBe(AnmeldeZustand.Geschlossen);
    }
}
