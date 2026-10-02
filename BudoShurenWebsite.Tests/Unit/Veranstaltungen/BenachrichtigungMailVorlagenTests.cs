using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class BenachrichtigungMailVorlagenTests
{
    private static readonly Veranstaltung V = new() { Id = 3, Titel = "Herbst <seminar>", ZusammenfassungUhrzeit = new TimeOnly(7, 30) };
    private static readonly VeranstaltungsTag Tag = new() { Id = 1, Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0) };

    private static readonly BenachrichtigungsAbschnitt Abschnitt = new("Max <b>Muster</b>", 3, ["Sa 14.11."],
        [new BenachrichtigungsZeile(new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), "Bestätigt")]);

    [Fact]
    public void Benachrichtigung_kodiert_Eingaben_und_zeigt_Stand()
    {
        var mail = BenachrichtigungMailVorlagen.Benachrichtigung(V, false, [Abschnitt], true, false, [new TagStand(Tag, 18, 20)],
            "https://test.example/Account/Member/Veranstaltungen/3", null);

        mail.Betreff.ShouldBe("Benachrichtigung: Herbst <seminar> (ausgebucht)");
        mail.Html.ShouldNotContain("<b>Muster</b>");
        mail.Html.ShouldContain("Max &lt;b&gt;Muster&lt;/b&gt;");
        mail.Html.ShouldContain("3 Personen, Sa 14.11.");
        mail.Html.ShouldContain("Bestätigt <span style=\"color:#555;font-size:12px;\">(01.10. 12:00)</span>");
        mail.Html.ShouldContain("ausgebucht");
        mail.Html.ShouldContain("18 von 20 Plätzen belegt");
        mail.Html.ShouldContain("https://test.example/Account/Member/Veranstaltungen/3");
        mail.Html.ShouldNotContain("abmelden");
    }

    [Fact]
    public void Externe_bekommen_Abmeldelink_statt_Link_zur_Uebersicht()
    {
        var mail = BenachrichtigungMailVorlagen.Benachrichtigung(V, true, [Abschnitt], false, true, [], null,
            "https://test.example/veranstaltungen/benachrichtigung-abmelden/abc");

        mail.Betreff.ShouldBe("Zusammenfassung: Herbst <seminar>");
        mail.Html.ShouldContain("Anmeldeschluss ist erreicht");
        mail.Html.ShouldNotContain("Zur Übersicht");
        mail.Html.ShouldContain("href=\"https://test.example/veranstaltungen/benachrichtigung-abmelden/abc\"");
    }

    [Fact]
    public void Eingetragen_nennt_wer_wofuer_und_wie_abmelden()
    {
        var mail = BenachrichtigungMailVorlagen.Eingetragen(V, "Olga <Orga>",
            BenachrichtigungEreignisse.NeueAnmeldung | BenachrichtigungEreignisse.Abmeldung, BenachrichtigungModus.TaeglicheZusammenfassung,
            "https://test.example/veranstaltungen/benachrichtigung-abmelden/xyz");

        mail.Html.ShouldContain("Olga &lt;Orga&gt; hat diese Adresse");
        mail.Html.ShouldContain("einmal täglich gegen 07:30 Uhr");
        mail.Html.ShouldContain("Neue Anmeldung, Abmeldung.");
        mail.Html.ShouldContain("benachrichtigung-abmelden/xyz");
    }
}
