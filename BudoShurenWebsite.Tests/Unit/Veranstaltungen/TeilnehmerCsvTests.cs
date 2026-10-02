using System.Text;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class TeilnehmerCsvTests
{
    private static readonly VeranstaltungsTag[] Tage =
    [
        new() { Id = 1, Datum = new DateOnly(2026, 11, 14) },
        new() { Id = 2, Datum = new DateOnly(2026, 11, 15) },
        new() { Id = 3, Datum = new DateOnly(2026, 11, 16), Abgesagt = true }
    ];

    private static Anmeldung Anmeldung(string nachname, params int[] tage)
    {
        var a = new Anmeldung
        {
            Vorname = "Max",
            Nachname = nachname,
            Email = $"{nachname.ToLowerInvariant()}@example.org",
            Status = AnmeldungStatus.Angemeldet,
            AnzahlBegleitpersonen = 1,
            ErstelltUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc)
        };
        foreach (var id in tage)
            a.Tage.Add(new AnmeldungTag { VeranstaltungsTagId = id });
        return a;
    }

    private static string[] Zeilen(byte[] csv) =>
        Encoding.UTF8.GetString(csv[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Mit_BOM_Kopfzeile_und_Semikolon()
    {
        var csv = TeilnehmerCsv.Erstellen([Anmeldung("Müller")], Tage, Teilnahmemodus.NurGesamt);

        csv[..3].ShouldBe(Encoding.UTF8.GetPreamble());
        var zeilen = Zeilen(csv);
        zeilen[0].ShouldStartWith("\"Nachname\";\"Vorname\";\"E-Mail\"");
        zeilen[1].ShouldStartWith("\"Müller\";\"Max\";\"müller@example.org\";\"Angemeldet\";\"2\";\"1\";\"Sa 14.11., So 15.11.\"");
        zeilen[1].ShouldContain("\"01.10.2026 12:00\"", customMessage: "Anmeldezeit in Ortszeit");
    }

    [Fact]
    public void Bei_Teilanmeldung_nur_die_gebuchten_Termine_chronologisch_und_Namen_alphabetisch()
    {
        var zeilen = Zeilen(TeilnehmerCsv.Erstellen([Anmeldung("Zander", 2), Anmeldung("Adler", 1, 2)], Tage, Teilnahmemodus.EinzelneTage));

        zeilen[1].ShouldContain("\"Adler\"");
        zeilen[1].ShouldContain("\"Sa 14.11., So 15.11.\"");
        zeilen[2].ShouldContain("\"Zander\"");
        zeilen[2].ShouldContain("\"So 15.11.\"");
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://boese.example\")", "\"'=HYPERLINK(\"\"http://boese.example\"\")\"")]
    [InlineData("+49 821 1234", "\"'+49 821 1234\"")]
    [InlineData("@SUMME(A1)", "\"'@SUMME(A1)\"")]
    [InlineData("Dojo \"Nord\"; Halle 2", "\"Dojo \"\"Nord\"\"; Halle 2\"")]
    public void Formeln_werden_entschaerft_und_Sonderzeichen_maskiert(string verein, string erwartet)
    {
        var anmeldung = Anmeldung("Muster");
        anmeldung.Verein = verein;

        Zeilen(TeilnehmerCsv.Erstellen([anmeldung], Tage, Teilnahmemodus.NurGesamt))[1].ShouldContain(erwartet);
    }
}
