using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class VeranstaltungMailVorlagenTests
{
    private static readonly VeranstaltungsTag[] Tage =
    [
        new() { Id = 1, Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(10, 0), Ende = new TimeOnly(16, 0) },
        new() { Id = 2, Datum = new DateOnly(2026, 11, 15), Beginn = new TimeOnly(9, 30), Ende = new TimeOnly(12, 0), Titel = "Prüfung" }
    ];

    private static Veranstaltung Veranstaltung() => new()
    {
        Titel = "Herbstseminar <Aikido>",
        Slug = "herbstseminar",
        Ort = "Dojo Göggingen",
        KontaktEmail = "seminar@example.org"
    };

    private static Anmeldung Anmeldung() => new()
    {
        Vorname = "<script>alert(1)</script>",
        Nachname = "Müller",
        Email = "max@example.org",
        AnzahlBegleitpersonen = 2,
        Telefon = "0821 999",
        Bemerkung = "Kaufen Sie günstige Uhren!"
    };

    [Fact]
    public void Eingaben_werden_HTML_kodiert_Umlaute_bleiben_lesbar()
    {
        var mail = VeranstaltungMailVorlagen.OptIn(Veranstaltung(), Tage, Anmeldung(), "https://x.de/veranstaltungen/bestaetigen/abc", 24);

        mail.Html.ShouldNotContain("<script>");
        mail.Html.ShouldContain("&lt;script&gt;");
        mail.Html.ShouldContain("Herbstseminar &lt;Aikido&gt;");
        mail.Html.ShouldContain("Müller");
        mail.Html.ShouldContain("Göggingen");
        mail.Betreff.ShouldBe("Bitte bestätige deine Anmeldung: Herbstseminar <Aikido>");
    }

    [Fact]
    public void OptIn_enthaelt_Link_Reservierung_und_Zusammenfassung()
    {
        var mail = VeranstaltungMailVorlagen.OptIn(Veranstaltung(), Tage, Anmeldung(), "https://x.de/veranstaltungen/bestaetigen/abc", 24);

        mail.Html.ShouldContain("href=\"https://x.de/veranstaltungen/bestaetigen/abc\"");
        mail.Html.ShouldContain("24 Stunden");
        // Nach Datum gruppiert: Datum fett, darunter Uhrzeit und Titel
        mail.Html.ShouldContain("<strong>Samstag, 14. November 2026</strong><br>10:00 – 16:00 Uhr</p>");
        mail.Html.ShouldContain("<strong>Sonntag, 15. November 2026</strong><br>09:30 – 12:00 Uhr · Prüfung</p>");
        mail.Html.ShouldContain("3 Personen (du und 2 Begleitpersonen)");
    }

    [Fact]
    public void Mehrere_Termine_an_einem_Datum_stehen_unter_einer_Ueberschrift()
    {
        VeranstaltungsTag[] tage =
        [
            new() { Id = 3, Datum = new DateOnly(2026, 11, 14), Beginn = new TimeOnly(19, 0), Titel = "Essen bei <Luigi>" },
            Tage[0]
        ];

        var mail = VeranstaltungMailVorlagen.OptIn(Veranstaltung(), tage, Anmeldung(), "https://x.de/b", 24);

        mail.Html.ShouldContain("<strong>Samstag, 14. November 2026</strong><br>10:00 – 16:00 Uhr<br>ab 19:00 Uhr · Essen bei &lt;Luigi&gt;</p>");
    }

    [Fact]
    public void Bei_Teilanmeldung_nur_die_gebuchten_Tage()
    {
        var anmeldung = Anmeldung();
        anmeldung.Tage.Add(new AnmeldungTag { VeranstaltungsTagId = 2 });

        var mail = VeranstaltungMailVorlagen.Bestaetigung(Veranstaltung(), Tage, anmeldung, "https://x.de/veranstaltungen/meine-anmeldung/abc");

        mail.Html.ShouldContain("15. November 2026");
        mail.Html.ShouldNotContain("14. November 2026");
        mail.Html.ShouldContain("href=\"https://x.de/veranstaltungen/meine-anmeldung/abc\"");
    }

    [Fact]
    public void Info_Mail_enthaelt_keine_freien_Texte_oder_Kontaktdaten_des_Anmelders()
    {
        var mail = VeranstaltungMailVorlagen.InfoAnBegleitung(Veranstaltung(), Tage, Anmeldung(), "https://x.de/veranstaltungen/herbstseminar", "https://x.de/veranstaltungen/info-abmelden/xyz");

        mail.Html.ShouldNotContain("Uhren");
        mail.Html.ShouldNotContain("0821");
        mail.Html.ShouldNotContain("max@example.org");
        mail.Html.ShouldContain("href=\"https://x.de/veranstaltungen/info-abmelden/xyz\"");
    }

    [Fact]
    public void Hinweis_an_die_alte_Adresse_nennt_die_neue_kodiert()
    {
        var mail = VeranstaltungMailVorlagen.EmailWechselHinweis(Veranstaltung(), Anmeldung(), "neu<b>@example.org");

        mail.Html.ShouldContain("neu&lt;b&gt;@example.org");
        mail.Html.ShouldContain("mailto:seminar@example.org");
    }

    [Fact]
    public void Angeforderte_Links_alle_in_einer_Mail()
    {
        var mail = VeranstaltungMailVorlagen.LinksAngefordert(
        [
            new("Herbstseminar", "14.11. – 15.11.2026", "https://x.de/veranstaltungen/meine-anmeldung/a", NochUnbestaetigt: false),
            new("Sommerfest", "01.07.2027", "https://x.de/veranstaltungen/bestaetigen/b", NochUnbestaetigt: true)
        ]);

        mail.Html.ShouldContain("href=\"https://x.de/veranstaltungen/meine-anmeldung/a\"");
        mail.Html.ShouldContain("href=\"https://x.de/veranstaltungen/bestaetigen/b\"");
        mail.Html.ShouldContain("Meine Anmeldung");
        mail.Html.ShouldContain("Anmeldung bestätigen");
    }

    [Fact]
    public void Zeitraum_ueber_die_nicht_abgesagten_Tage()
    {
        VeranstaltungMailVorlagen.Zeitraum(Tage).ShouldBe("14.–15. November 2026");
        VeranstaltungMailVorlagen.Zeitraum([Tage[0]]).ShouldBe("14. November 2026");
        VeranstaltungMailVorlagen.Zeitraum([]).ShouldBe("");
    }

    [Fact]
    public void Ablehnung_durch_Organisator_mit_kodiertem_Grund()
    {
        var mit = VeranstaltungMailVorlagen.AbgelehntDurchOrganisator(Veranstaltung(), Anmeldung(), "Nur <b>Mitglieder</b>");
        var ohne = VeranstaltungMailVorlagen.AbgelehntDurchOrganisator(Veranstaltung(), Anmeldung(), null);

        mit.Html.ShouldContain("Nur &lt;b&gt;Mitglieder&lt;/b&gt;");
        ohne.Html.ShouldNotContain("border-left:3px solid #ccc;padding-left:12px;\">");
        ohne.Html.ShouldContain("mailto:seminar@example.org");
        // Absender ist die System-Adresse (noreply); auf "antworte auf diese Mail" verweisen wir deshalb nicht
        ohne.Html.ShouldNotContain("Antwort auf diese");
    }

    [Fact]
    public void Nachricht_mit_Anrede_Inhalt_Kontakt_und_Fuss()
    {
        var teilnehmer = VeranstaltungMailVorlagen.Nachricht(Veranstaltung(), "Treffpunkt", "<p>Halle 2</p>", "<b>Max</b>",
            VeranstaltungMailVorlagen.FussTeilnehmer("https://x.de/veranstaltungen/link-anfordern"));
        var info = VeranstaltungMailVorlagen.Nachricht(Veranstaltung(), "Treffpunkt", "<p>Halle 2</p>", null,
            VeranstaltungMailVorlagen.FussInfo("https://x.de/veranstaltungen/info-abmelden/abc"));

        teilnehmer.Betreff.ShouldBe("Treffpunkt");
        teilnehmer.Html.ShouldContain("Hallo &lt;b&gt;Max&lt;/b&gt;,");
        teilnehmer.Html.ShouldContain("<p>Halle 2</p>");
        teilnehmer.Html.ShouldContain("href=\"https://x.de/veranstaltungen/link-anfordern\"");
        info.Html.ShouldContain("<p>Hallo,</p>");
        info.Html.ShouldContain("href=\"https://x.de/veranstaltungen/info-abmelden/abc\"");
    }

    [Fact]
    public void Absage_eines_Tages_nennt_den_Termin()
    {
        VeranstaltungMailVorlagen.TagAbgesagtInhalt(Tage[1], "<p>Zusatz</p>")
            .ShouldBe("<p><strong>Der Termin am Sonntag, 15. November 2026, 09:30 – 12:00 Uhr (Prüfung) wurde abgesagt.</strong> Die übrigen Termine finden wie geplant statt.</p><p>Zusatz</p>");
    }

    [Fact]
    public void Ablehnung_ist_neutral_und_nennt_den_Kontakt()
    {
        var mail = VeranstaltungMailVorlagen.AnmeldungNichtMoeglich(Veranstaltung());

        mail.Html.ShouldContain("mailto:seminar@example.org");
        mail.Html.ShouldNotContain("abgelehnt", Case.Insensitive);
    }
}
