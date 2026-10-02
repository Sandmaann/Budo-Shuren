using Bunit;
using BudoShurenWebsite.Components.Shared.Veranstaltungen;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;
using NSubstitute;

namespace BudoShurenWebsite.Tests.Komponenten;

[Trait("Category", "Komponente")]
public class AnmeldungPanelTests : BunitContext
{
    private const int VeranstaltungId = 7;
    private const int AnmeldungId = 42;
    private const string Basis = "https://test.example/";

    private static readonly VerwaltungsBenutzer Admin = new("u1", "Olga", IstAdmin: true, IstAbteilungsleiter: false, Abteilung: null);
    private static readonly AnmeldeFormularEinstellungen Einstellungen = new(Teilnahmemodus.NurGesamt, 1, 2,
        FormularFeldModus.Optional, FormularFeldModus.Optional, FormularFeldModus.Optional, FormularFeldModus.Optional);

    private readonly ITeilnehmerVerwaltungService _service = Substitute.For<ITeilnehmerVerwaltungService>();
    private int _geaendert;

    public AnmeldungPanelTests() => Services.AddSingleton(_service);

    private static AnmeldungDetail Detail(AnmeldungStatus status = AnmeldungStatus.Angemeldet, bool ungesehen = false, string? notiz = null) =>
        new(AnmeldungId, status, status == AnmeldungStatus.Abgelehnt ? "Zu spät" : null, AnmeldungQuelle.Formular, "0170 123", null, notiz, null,
            ["info@example.org"], [], new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), null,
            new AnmeldeEingabe { Vorname = "Max", Nachname = "Muster", Email = "max@example.org", AnzahlBegleitpersonen = 1, InfoEmails = "info@example.org" },
            [new EreignisAnzeige(AnmeldungId, "Max Muster", new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), EreignisAkteur.Teilnehmer, null,
                AnmeldungEreignisArt.Angelegt, "Angemeldet", ungesehen)]);

    private void DetailLiefert(AnmeldungDetail? erstes, params AnmeldungDetail?[] weitere) =>
        _service.DetailAsync(VeranstaltungId, AnmeldungId, Admin, Arg.Any<CancellationToken>()).Returns(erstes, weitere);

    private IRenderedComponent<AnmeldungPanel> Panel() =>
        Render<AnmeldungPanel>(p => p
            .Add(x => x.VeranstaltungId, VeranstaltungId)
            .Add(x => x.AnmeldungId, AnmeldungId)
            .Add(x => x.Benutzer, Admin)
            .Add(x => x.BasisUrl, Basis)
            .Add(x => x.Einstellungen, Einstellungen)
            .Add(x => x.TagInfos, [])
            .Add(x => x.OnGeaendert, () => _geaendert++));

    private static void Klick(IRenderedComponent<AnmeldungPanel> panel, string text) =>
        panel.FindAll("button").Single(b => b.TextContent.Trim() == text).Click();

    private static IEnumerable<string> Knoepfe(IRenderedComponent<AnmeldungPanel> panel) =>
        panel.FindAll("button").Select(b => b.TextContent.Trim());

    [Fact]
    public void Zeigt_die_Daten_und_markiert_Ungesehenes_als_gesehen()
    {
        DetailLiefert(Detail(ungesehen: true));

        var panel = Panel();

        panel.Find("h3").TextContent.ShouldBe("Max Muster");
        panel.Markup.ShouldContain("0170 123");
        panel.Markup.ShouldContain("info@example.org");
        panel.Markup.ShouldContain("2 (inkl. 1 Begleitung)");
        panel.Find("li").TextContent.ShouldContain("neu", customMessage: "in diesem Durchgang noch hervorgehoben");
        _service.Received(1).AlsGesehenMarkierenAsync(VeranstaltungId,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { AnmeldungId })), Admin, Arg.Any<CancellationToken>());
        _geaendert.ShouldBe(1);
    }

    [Fact]
    public void Ohne_Ungesehenes_wird_nichts_markiert()
    {
        DetailLiefert(Detail());

        Panel();

        _service.DidNotReceiveWithAnyArgs().AlsGesehenMarkierenAsync(default, default, default!, Arg.Any<CancellationToken>());
        _geaendert.ShouldBe(0);
    }

    [Fact]
    public void Nicht_gefunden()
    {
        DetailLiefert(null);

        Panel().Markup.ShouldContain("Die Anmeldung wurde nicht gefunden.");
    }

    [Fact]
    public void Ablehnen_mit_Grund_laedt_neu_und_meldet_die_Aenderung()
    {
        DetailLiefert(Detail(), Detail(AnmeldungStatus.Abgelehnt));
        _service.AblehnenAsync(VeranstaltungId, AnmeldungId, "Zu spät", true, Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.Ok(AnmeldungId));
        var panel = Panel();

        Klick(panel, "Ablehnen...");
        panel.Find("#ablehnungsgrund").Change("Zu spät");
        Klick(panel, "Anmeldung ablehnen");

        panel.Markup.ShouldContain("Die Anmeldung wurde abgelehnt.");
        panel.Markup.ShouldContain("Grund: Zu spät");
        Knoepfe(panel).ShouldContain("Ablehnung zurücknehmen");
        Knoepfe(panel).ShouldNotContain("Ablehnen...");
        _geaendert.ShouldBe(1);
    }

    [Fact]
    public void Fehler_des_Service_werden_angezeigt_ohne_neu_zu_laden()
    {
        DetailLiefert(Detail(AnmeldungStatus.Abgelehnt));
        _service.AblehnungZuruecknehmenAsync(VeranstaltungId, AnmeldungId, Basis, Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.MitFehler("Kein Platz mehr frei."));
        var panel = Panel();

        Klick(panel, "Ablehnung zurücknehmen");

        panel.Markup.ShouldContain("Kein Platz mehr frei.");
        panel.Markup.IndexOf("Kein Platz mehr frei.").ShouldBeGreaterThan(panel.Markup.IndexOf("Ablehnung zurücknehmen"));
        _service.Received(1).DetailAsync(VeranstaltungId, AnmeldungId, Admin, Arg.Any<CancellationToken>());
        _geaendert.ShouldBe(0);
    }

    [Fact]
    public void Bearbeiten_uebernimmt_die_Daten_und_speichert_die_Aenderung()
    {
        DetailLiefert(Detail());
        AnmeldeEingabe? gespeichert = null;
        _service.BearbeitenAsync(VeranstaltungId, AnmeldungId, Arg.Do<AnmeldeEingabe>(e => gespeichert = e), false, Basis, Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.Ok(AnmeldungId));
        var panel = Panel();

        Klick(panel, "Bearbeiten");
        panel.Find("#bearbeiten-nachname").Change("Mustermann");
        panel.FindAll("input[type=checkbox]").Single().Change(false);
        panel.Find("form").Submit();

        gespeichert.ShouldNotBeNull();
        gespeichert.Nachname.ShouldBe("Mustermann");
        gespeichert.Email.ShouldBe("max@example.org");
        gespeichert.InfoEmails.ShouldBe("info@example.org");
        panel.Markup.ShouldContain("Gespeichert.");
        panel.FindAll("form").ShouldBeEmpty();
    }

    [Fact]
    public void Notiz_speichern()
    {
        DetailLiefert(Detail(notiz: "alt"));
        _service.NotizSpeichernAsync(VeranstaltungId, AnmeldungId, "Zahlt bar", Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.Ok(AnmeldungId));
        var panel = Panel();
        panel.Find("#notiz").GetAttribute("value").ShouldBe("alt");

        panel.Find("#notiz").Change("Zahlt bar");
        Klick(panel, "Notiz speichern");

        panel.Markup.ShouldContain("Notiz gespeichert.");
        // Rückmeldung beim Button, nicht oben im Dialog
        panel.Markup.IndexOf("Notiz gespeichert.").ShouldBeGreaterThan(panel.Markup.IndexOf("Notiz speichern"));
    }
}
