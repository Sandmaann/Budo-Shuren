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
        var zeilen = text.ReplaceLineEndings("\n").Split('\n').Where(z => z.Length > 0).ToArray();
        zeilen[1..5].ShouldBe(["**Samstag, 14. November 2026**", "- 10:00 – 16:30 Uhr", "**Sonntag, 15. November 2026**", "- 10:00 – 16:30 Uhr · Kata"]);
        text.ShouldNotContain("16. November");
        text.ShouldContain("**Ort:** Halle Nord");
    }

    [Fact]
    public void Ohne_Ort_keine_Ortszeile() =>
        NachrichtVorlagen.Terminaenderung(Uebersicht(" ", Tag(1, 14))).Text.ShouldNotContain("Ort:");

    [Theory]
    [InlineData("Hallo zusammen,\n\nder Termin steht.", true)]
    [InlineData("  hallo", true)]
    [InlineData("Hi Leute", true)]
    [InlineData("Liebe Teilnehmer", true)]
    [InlineData("Lieber Max", true)]
    [InlineData("Guten Morgen!", true)]
    [InlineData("Sehr geehrte Damen und Herren", true)]
    [InlineData("Grüß Gott", true)]
    [InlineData("Hinweis: der Termin steht.", false)]
    [InlineData("Hallenwechsel: wir trainieren in Halle 2.", false)]
    [InlineData("bei der Veranstaltung haben sich Termin oder Ort geändert.", false)]
    [InlineData("Viele liebe Grüße", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Erkennt_eine_Anrede_am_Textanfang(string? text, bool erwartet) =>
        NachrichtVorlagen.BeginntMitAnrede(text).ShouldBe(erwartet);
}
