using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class KapazitaetsRechnerTests
{
    private static readonly DateTime Jetzt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    // Sa (10 Plätze), So (5 Plätze), Mo (unbegrenzt)
    private static readonly TagKapazitaet[] Tage =
    [
        new(TagId: 1, MaxTeilnehmer: 10, Abgesagt: false),
        new(TagId: 2, MaxTeilnehmer: 5, Abgesagt: false),
        new(TagId: 3, MaxTeilnehmer: null, Abgesagt: false)
    ];

    private static AnmeldungBelegung Angemeldet(int id, int begleitung = 0, params int[] tage) =>
        new(id, AnmeldungStatus.Angemeldet, null, begleitung, tage);

    [Fact]
    public void NurGesamt_zaehlt_Anmelder_und_Begleitung_an_allen_Tagen()
    {
        var belegung = KapazitaetsRechner.BelegungProTag(Teilnahmemodus.NurGesamt, Tage, [Angemeldet(1, begleitung: 2)], Jetzt);

        belegung.ShouldBe(new Dictionary<int, int> { [1] = 3, [2] = 3, [3] = 3 });
    }

    [Fact]
    public void EinzelneTage_zaehlt_nur_gebuchte_Tage()
    {
        var belegung = KapazitaetsRechner.BelegungProTag(
            Teilnahmemodus.EinzelneTage, Tage, [Angemeldet(1, 1, 1), Angemeldet(2, 0, 1, 2)], Jetzt);

        belegung.ShouldBe(new Dictionary<int, int> { [1] = 3, [2] = 1, [3] = 0 });
    }

    [Theory]
    [InlineData(AnmeldungStatus.Angemeldet, null, true)]
    [InlineData(AnmeldungStatus.Unbestaetigt, 1, true)]   // Reservierung läuft noch eine Stunde
    [InlineData(AnmeldungStatus.Unbestaetigt, -1, false)] // Reservierung abgelaufen
    [InlineData(AnmeldungStatus.Unbestaetigt, null, false)]
    [InlineData(AnmeldungStatus.Warteliste, null, false)]
    [InlineData(AnmeldungStatus.Storniert, null, false)]
    [InlineData(AnmeldungStatus.Abgelehnt, null, false)]
    public void Nur_bestaetigte_und_laufend_reservierte_Anmeldungen_belegen_Plaetze(
        AnmeldungStatus status, int? reserviertStunden, bool belegt)
    {
        DateTime? reserviertBis = reserviertStunden is { } h ? Jetzt.AddHours(h) : null;

        KapazitaetsRechner.BelegtPlaetze(status, reserviertBis, Jetzt).ShouldBe(belegt);
    }

    [Fact]
    public void Abgesagte_Tage_werden_weder_gezaehlt_noch_gebucht()
    {
        TagKapazitaet[] tage = [new(1, 10, false), new(2, 5, Abgesagt: true)];

        var belegung = KapazitaetsRechner.BelegungProTag(Teilnahmemodus.NurGesamt, tage, [Angemeldet(1)], Jetzt);

        belegung.ShouldBe(new Dictionary<int, int> { [1] = 1 });
    }

    [Fact]
    public void Freie_Plaetze_sind_nie_negativ_und_null_bei_unbegrenzt()
    {
        // Überbuchung, z. B. nachdem der Admin die Kapazität gesenkt hat
        var frei = KapazitaetsRechner.FreiePlaetzeProTag(Teilnahmemodus.NurGesamt, Tage, [Angemeldet(1, begleitung: 6)], Jetzt);

        frei.ShouldBe(new Dictionary<int, int?> { [1] = 3, [2] = 0, [3] = null });
    }

    [Fact]
    public void Pruefen_meldet_alle_vollen_Tage_der_Anfrage()
    {
        var bestehend = new[] { Angemeldet(1, begleitung: 3) }; // belegt 4 an allen Tagen

        var ergebnis = KapazitaetsRechner.Pruefen(Teilnahmemodus.NurGesamt, Tage, bestehend, Jetzt, [], personen: 2);

        ergebnis.Passt.ShouldBeFalse();
        ergebnis.VolleTagIds.ShouldBe([2]); // So: 5 - 4 = 1 frei < 2
    }

    [Fact]
    public void Pruefen_genau_passend_ist_erlaubt()
    {
        var bestehend = new[] { Angemeldet(1, begleitung: 2) }; // So: 2 frei

        KapazitaetsRechner.Pruefen(Teilnahmemodus.NurGesamt, Tage, bestehend, Jetzt, [], personen: 2).Passt.ShouldBeTrue();
    }

    [Fact]
    public void Pruefen_bei_EinzelneTage_betrifft_nur_angefragte_Tage()
    {
        var bestehend = new[] { Angemeldet(1, 4, 2) }; // So voll

        KapazitaetsRechner.Pruefen(Teilnahmemodus.EinzelneTage, Tage, bestehend, Jetzt, [1, 3], personen: 1).Passt.ShouldBeTrue();
        KapazitaetsRechner.Pruefen(Teilnahmemodus.EinzelneTage, Tage, bestehend, Jetzt, [1, 2], personen: 1).VolleTagIds.ShouldBe([2]);
    }

    [Fact]
    public void Beim_Umbuchen_zaehlt_die_eigene_Anmeldung_nicht_mit()
    {
        var bestehend = new[] { Angemeldet(7, begleitung: 4) }; // So voll, aber nur durch Anmeldung 7

        KapazitaetsRechner.Pruefen(Teilnahmemodus.NurGesamt, Tage, bestehend, Jetzt, [], personen: 5).Passt.ShouldBeFalse();
        KapazitaetsRechner.Pruefen(Teilnahmemodus.NurGesamt, Tage, bestehend, Jetzt, [], personen: 5, ohneAnmeldungId: 7).Passt.ShouldBeTrue();
    }
}
