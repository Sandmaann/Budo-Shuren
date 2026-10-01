using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class NachrichtVorlagenTests
{
    private static VeranstaltungUebersicht Uebersicht(string? ort, params TagBelegung[] tage) =>
        new(1, "Herbstseminar", "herbstseminar", VeranstaltungStatus.Veroeffentlicht, VeranstaltungSichtbarkeit.Oeffentlich,
            Teilnahmemodus.NurGesamt, null, ort,
            new AnmeldeFormularEinstellungen(Teilnahmemodus.NurGesamt, 1, 2, FormularFeldModus.Optional, FormularFeldModus.Optional,
                FormularFeldModus.Optional, FormularFeldModus.Optional),
            tage, [], []);

    private static TagBelegung Tag(int id, int tagImMonat, string? titel = null, bool abgesagt = false) =>
        new(id, new DateOnly(2026, 11, tagImMonat), new TimeOnly(10, 0), new TimeOnly(16, 30), titel, abgesagt, 20, 0);

    [Fact]
    public void Terminaenderung_listet_stattfindende_Tage_in_Reihenfolge_und_den_Ort()
    {
        var (betreff, text) = NachrichtVorlagen.Terminaenderung(Uebersicht("Halle Nord",
            Tag(2, 15, "Kata"), Tag(1, 14), Tag(3, 16, abgesagt: true)));

        betreff.ShouldBe("Änderung: Herbstseminar");
        var termine = text.ReplaceLineEndings("\n").Split('\n').Where(z => z.StartsWith("- ")).ToArray();
        termine.ShouldBe(["- Samstag, 14.11.2026, 10:00–16:30 Uhr", "- Sonntag, 15.11.2026, 10:00–16:30 Uhr (Kata)"]);
        text.ShouldNotContain("16.11.2026");
        text.ShouldContain("**Ort:** Halle Nord");
    }

    [Fact]
    public void Ohne_Ort_keine_Ortszeile() =>
        NachrichtVorlagen.Terminaenderung(Uebersicht(" ", Tag(1, 14))).Text.ShouldNotContain("Ort:");
}
