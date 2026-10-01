using AngleSharp.Dom;
using Bunit;
using BudoShurenWebsite.Components.Shared.Veranstaltungen;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;
using NSubstitute;

namespace BudoShurenWebsite.Tests.Komponenten;

[Trait("Category", "Komponente")]
public class RundmailBereichTests : BunitContext
{
    private const int VeranstaltungId = 7;
    private const string Basis = "https://test.example/";
    private static readonly VerwaltungsBenutzer Admin = new("u1", "Olga", IstAdmin: true, IstAbteilungsleiter: false, Abteilung: null);

    private readonly IVeranstaltungKommunikationService _service = Substitute.For<IVeranstaltungKommunikationService>();

    public RundmailBereichTests()
    {
        Services.AddSingleton(_service);
        _service.NachrichtenAsync(VeranstaltungId, Admin, Arg.Any<CancellationToken>()).Returns(Array.Empty<NachrichtAnzeige>());
    }

    private IRenderedComponent<RundmailBereich> Bereich(int teilnehmer = 3, string? eigeneEmail = "olga@example.org", string? betreff = null, string? text = null) =>
        Render<RundmailBereich>(p => p
            .Add(x => x.VeranstaltungId, VeranstaltungId)
            .Add(x => x.Benutzer, Admin)
            .Add(x => x.BasisUrl, Basis)
            .Add(x => x.AnzahlTeilnehmer, teilnehmer)
            .Add(x => x.EigeneEmail, eigeneEmail)
            .Add(x => x.VorlageBetreff, betreff)
            .Add(x => x.VorlageText, text));

    private static IElement Knopf(IRenderedComponent<RundmailBereich> bereich, string text) =>
        bereich.FindAll("button").Single(b => b.TextContent.Trim() == text);

    [Fact]
    public void Vorlage_wird_uebernommen_und_als_Vorschau_gezeigt()
    {
        var bereich = Bereich(betreff: "Änderung: Herbstseminar", text: "**Ort:** Halle");

        bereich.Find("#rundmail-betreff").GetAttribute("value").ShouldBe("Änderung: Herbstseminar");
        bereich.Find("details").InnerHtml.ShouldContain("<strong>Ort:</strong> Halle");
        bereich.Markup.ShouldContain("Noch keine.");
    }

    [Fact]
    public void Senden_erst_nach_Rueckfrage_dann_Felder_leer_und_Protokoll_neu()
    {
        _service.RundmailSendenAsync(VeranstaltungId, "Hallo", "Text", true, Basis, Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.Ok(1));
        var bereich = Bereich();
        bereich.Find("#rundmail-betreff").Input("Hallo");
        bereich.Find("#rundmail-text").Input("Text");
        bereich.Find("input[type=checkbox]").Change(true);

        Knopf(bereich, "Senden...").Click();
        _service.DidNotReceiveWithAnyArgs().RundmailSendenAsync(default, default!, default!, default, default!, default!, Arg.Any<CancellationToken>());
        bereich.Markup.ShouldContain("Die Nachricht an 3 bestätigte Teilnehmer und ihre Info-Adressen senden?");

        Knopf(bereich, "Jetzt senden").Click();

        bereich.Markup.ShouldContain("Die Rundmail ist unterwegs.");
        bereich.Find("#rundmail-betreff").GetAttribute("value").ShouldBeNullOrEmpty();
        _service.Received(2).NachrichtenAsync(VeranstaltungId, Admin, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Fehler_beim_Senden_bleiben_mit_Eingaben_stehen()
    {
        _service.RundmailSendenAsync(default, default!, default!, default, default!, default!, Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(VerwaltungsErgebnis.MitFehler("Bitte einen Betreff angeben."));
        var bereich = Bereich();
        bereich.Find("#rundmail-text").Input("Text");

        Knopf(bereich, "Senden...").Click();
        Knopf(bereich, "Jetzt senden").Click();

        bereich.Markup.ShouldContain("Bitte einen Betreff angeben.");
        bereich.Find("#rundmail-text").GetAttribute("value").ShouldBe("Text");
    }

    [Fact]
    public void Testmail_an_die_eigene_Adresse()
    {
        _service.TestmailSendenAsync(VeranstaltungId, "Hallo", "", "olga@example.org", Basis, Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.Ok(1));
        var bereich = Bereich();
        bereich.Find("#rundmail-betreff").Input("Hallo");

        Knopf(bereich, "Testmail an mich").Click();

        bereich.Markup.ShouldContain("Die Testmail an olga@example.org ist unterwegs.");
    }

    [Fact]
    public void Ohne_eigene_Adresse_oder_ohne_Teilnehmer_gesperrt()
    {
        var bereich = Bereich(teilnehmer: 0, eigeneEmail: null);

        Knopf(bereich, "Testmail an mich").HasAttribute("disabled").ShouldBeTrue();
        Knopf(bereich, "Senden...").HasAttribute("disabled").ShouldBeTrue();
        bereich.Markup.ShouldContain("Es gibt noch keine bestätigten Teilnehmer.");
    }

    [Fact]
    public void Protokoll_zeigt_gesendete_Nachrichten()
    {
        _service.NachrichtenAsync(VeranstaltungId, Admin, Arg.Any<CancellationToken>()).Returns(
        [
            new NachrichtAnzeige(1, NachrichtArt.Rundmail, "Anreise", 12, true, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), "Olga")
        ]);

        var eintrag = Bereich().Find("li").TextContent;

        eintrag.ShouldContain("01.10.2026 12:00");
        eintrag.ShouldContain("Olga");
        eintrag.ShouldContain("Anreise");
        eintrag.ShouldContain("12 Empfänger (inkl. Info-Adressen)");
    }
}
