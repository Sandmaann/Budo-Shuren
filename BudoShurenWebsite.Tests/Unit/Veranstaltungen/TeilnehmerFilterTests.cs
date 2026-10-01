using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class TeilnehmerFilterTests
{
    private static TeilnehmerZeile Zeile(int id, string vorname, string nachname, AnmeldungStatus status = AnmeldungStatus.Angemeldet,
        bool neu = false, string? verein = null) =>
        new(id, vorname, nachname, $"{vorname.ToLowerInvariant()}@example.org", status, 0, [], verein, null,
            AnmeldungQuelle.Formular, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), neu, false);

    private static readonly TeilnehmerZeile[] Alle =
    [
        Zeile(1, "Max", "Muster"),
        Zeile(2, "Erika", "Adler", AnmeldungStatus.Unbestaetigt, neu: true),
        Zeile(3, "Otto", "Zander", AnmeldungStatus.Storniert, neu: true),
        Zeile(4, "Ida", "Berg", AnmeldungStatus.Abgelehnt, verein: "Dojo Nord"),
        Zeile(5, "Anna", "Muster")
    ];

    [Theory]
    [InlineData(TeilnehmerFilterArt.Aktiv, new[] { 2, 5, 1 })]
    [InlineData(TeilnehmerFilterArt.Neu, new[] { 2, 3 })]
    [InlineData(TeilnehmerFilterArt.Unbestaetigt, new[] { 2 })]
    [InlineData(TeilnehmerFilterArt.Abgemeldet, new[] { 3 })]
    [InlineData(TeilnehmerFilterArt.Abgelehnt, new[] { 4 })]
    [InlineData(TeilnehmerFilterArt.Alle, new[] { 2, 4, 5, 1, 3 })]
    public void Schnellfilter_sortiert_nach_Nachname_und_Vorname(TeilnehmerFilterArt art, int[] erwartet)
    {
        TeilnehmerFilter.Anwenden(Alle, art, null).Select(t => t.Id).ShouldBe(erwartet);
        TeilnehmerFilter.Anzahl(Alle, art).ShouldBe(erwartet.Length);
    }

    [Theory]
    [InlineData("muster", new[] { 5, 1 })]
    [InlineData("  ERIKA ", new[] { 2 })]
    [InlineData("max muster", new[] { 1 })]
    [InlineData("otto@", new[] { 3 })]
    [InlineData("nord", new[] { 4 })]
    [InlineData("niemand", new int[0])]
    public void Suche_in_Name_EMail_und_Verein(string suche, int[] erwartet) =>
        TeilnehmerFilter.Anwenden(Alle, TeilnehmerFilterArt.Alle, suche).Select(t => t.Id).ShouldBe(erwartet);

    [Fact]
    public void Suche_wirkt_zusammen_mit_dem_Filter() =>
        TeilnehmerFilter.Anwenden(Alle, TeilnehmerFilterArt.Neu, "otto").Select(t => t.Id).ShouldBe([3]);
}
