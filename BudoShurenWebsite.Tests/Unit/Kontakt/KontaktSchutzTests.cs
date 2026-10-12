using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Kontakt;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Unit.Kontakt;

[Trait("Category", "Unit")]
public class KontaktSchutzTests
{
    private const string Text = "Hallo, ich möchte gerne zum Probetraining kommen.";

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly KontaktSchutz _schutz;

    public KontaktSchutzTests()
    {
        _schutz = new KontaktSchutz(new FormularSchutz(new EphemeralDataProtectionProvider(), _zeit), new KontaktLimit(_zeit));
    }

    private KontaktPruefung Pruefen(string zeitstempel, string? honeypot = null, string text = Text, string email = "max@example.org") =>
        _schutz.Pruefen(honeypot, zeitstempel, email, text, "203.0.113.1");

    [Fact]
    public void Menschliches_Ausfuellen_wird_angenommen()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(30));

        Pruefen(zeitstempel).ShouldBe(KontaktPruefung.Angenommen);
    }

    [Fact]
    public void Ausgefuellter_Honeypot_wird_abgelehnt()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(30));

        Pruefen(zeitstempel, honeypot: "https://spam.example").Abgelehnt.ShouldBe(KontaktAnfrageStatus.Honeypot);
    }

    [Fact]
    public void Zu_schnelles_Absenden_wird_abgelehnt()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(FormularSchutz.Mindestdauer - TimeSpan.FromMilliseconds(1));

        Pruefen(zeitstempel).Abgelehnt.ShouldBe(KontaktAnfrageStatus.ZuSchnell);
    }

    [Theory]
    [InlineData("")]
    [InlineData("638000000000000000")] // unverschlüsselt, also selbst gebaut
    [InlineData("kaputt")]
    public void Fehlender_oder_manipulierter_Zeitstempel_wird_abgelehnt(string zeitstempel)
    {
        Pruefen(zeitstempel).Abgelehnt.ShouldBe(KontaktAnfrageStatus.FormularUngueltig);
    }

    [Fact]
    public void Alter_Zeitstempel_laesst_sich_nicht_beliebig_wiederverwenden()
    {
        var zeitstempel = _schutz.Zeitstempel();

        _zeit.Advance(KontaktSchutz.FormularGueltigFuer);
        Pruefen(zeitstempel).ShouldBe(KontaktPruefung.Angenommen);

        _zeit.Advance(TimeSpan.FromSeconds(1));
        var abgelaufen = Pruefen(zeitstempel);
        abgelaufen.Abgelehnt.ShouldBe(KontaktAnfrageStatus.FormularUngueltig);
        abgelaufen.Hinweis.ShouldBe("Zeitstempel abgelaufen");
    }

    [Fact]
    public void Werbung_wird_abgelehnt_und_der_Grund_steht_im_Hinweis()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(30));

        var pruefung = Pruefen(zeitstempel, text: "Mehr Besucher: https://a.example https://b.example");

        pruefung.Abgelehnt.ShouldBe(KontaktAnfrageStatus.Inhalt);
        pruefung.Hinweis.ShouldBe("2 Links");
    }

    [Fact]
    public void Zu_viele_Nachrichten_eines_Absenders_werden_abgelehnt()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(30));

        for (var i = 0; i < KontaktLimit.JeAbsender; i++)
            Pruefen(zeitstempel).ShouldBe(KontaktPruefung.Angenommen);

        Pruefen(zeitstempel).Abgelehnt.ShouldBe(KontaktAnfrageStatus.ZuVieleAnfragen);
    }

    [Fact]
    public void Als_Spam_erkannte_Nachrichten_verbrauchen_das_Kontingent_nicht()
    {
        var zeitstempel = _schutz.Zeitstempel();
        _zeit.Advance(TimeSpan.FromSeconds(30));

        for (var i = 0; i < 10; i++)
            Pruefen(zeitstempel, honeypot: "x").Abgelehnt.ShouldBe(KontaktAnfrageStatus.Honeypot);

        Pruefen(zeitstempel).ShouldBe(KontaktPruefung.Angenommen);
    }

    [Fact]
    public void Jede_Ablehnung_hat_eine_Meldung_und_nur_abgelehnte_Status_gelten_als_abgelehnt()
    {
        foreach (var status in Enum.GetValues<KontaktAnfrageStatus>())
        {
            KontaktSchutz.Meldung(status).ShouldNotBeNullOrWhiteSpace();
            status.Bezeichnung().ShouldNotBeNullOrWhiteSpace();
        }

        KontaktAnfrageStatus.Zugestellt.IstAbgelehnt().ShouldBeFalse();
        KontaktAnfrageStatus.VersandFehler.IstAbgelehnt().ShouldBeFalse();
        KontaktAnfrageStatus.Honeypot.IstAbgelehnt().ShouldBeTrue();
        KontaktAnfrageStatus.ZuVieleAnfragen.IstAbgelehnt().ShouldBeTrue();
    }
}
