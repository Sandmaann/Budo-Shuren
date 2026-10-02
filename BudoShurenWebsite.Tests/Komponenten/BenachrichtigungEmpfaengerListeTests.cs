using Bunit;
using BudoShurenWebsite.Components.Shared.Veranstaltungen;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;
using NSubstitute;

namespace BudoShurenWebsite.Tests.Komponenten;

[Trait("Category", "Komponente")]
public class BenachrichtigungEmpfaengerListeTests : BunitContext
{
    private const int VeranstaltungId = 7;
    private static readonly VerwaltungsBenutzer Admin = new("u1", "Olga", IstAdmin: true, IstAbteilungsleiter: false, Abteilung: null);

    private readonly IVeranstaltungVerwaltungService _service = Substitute.For<IVeranstaltungVerwaltungService>();

    public BenachrichtigungEmpfaengerListeTests()
    {
        Services.AddSingleton(_service);
        _service.MoeglicheEmpfaengerAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<BenutzerAuswahl>());
        _service.EmpfaengerAsync(VeranstaltungId, Admin, Arg.Any<CancellationToken>()).Returns(
        [
            new EmpfaengerAnzeige(3, "Olga", "olga@example.org", "u1", IstErsteller: true,
                BenachrichtigungEreignisse.Alle, BenachrichtigungModus.TaeglicheZusammenfassung, Abgemeldet: false)
        ]);
    }

    private IRenderedComponent<BenachrichtigungEmpfaengerListe> Liste() =>
        Render<BenachrichtigungEmpfaengerListe>(p => p
            .Add(x => x.VeranstaltungId, VeranstaltungId)
            .Add(x => x.Benutzer, Admin)
            .Add(x => x.ZusammenfassungUhrzeit, new TimeOnly(18, 30)));

    [Fact]
    public void Auswahl_Wann_erklaert_Sofort_und_Tageszusammenfassung_mit_Uhrzeit()
    {
        var auswahl = Liste().Find("#modus-3");

        auswahl.GetAttribute("value").ShouldBe(nameof(BenachrichtigungModus.TaeglicheZusammenfassung));
        var optionen = auswahl.QuerySelectorAll("option").Select(o => o.TextContent).ToList();
        optionen.ShouldBe(
        [
            "Sofort bei jeder Änderung (gesammelt, ca. 5 Minuten nach der letzten)",
            "Nur Tageszusammenfassung um 18:30 Uhr (nur wenn sich etwas getan hat)",
            "Pausiert (keine E-Mails)"
        ]);
    }

    [Fact]
    public void Moduswechsel_wird_gespeichert()
    {
        _service.EmpfaengerAendernAsync(VeranstaltungId, 3, BenachrichtigungEreignisse.Alle, BenachrichtigungModus.Sofort, Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.Ok(3));

        Liste().Find("#modus-3").Change(nameof(BenachrichtigungModus.Sofort));

        _service.Received(1).EmpfaengerAendernAsync(VeranstaltungId, 3, BenachrichtigungEreignisse.Alle, BenachrichtigungModus.Sofort, Admin, Arg.Any<CancellationToken>());
    }
}
