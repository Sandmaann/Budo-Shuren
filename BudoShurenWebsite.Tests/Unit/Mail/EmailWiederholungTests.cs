using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Mail;

namespace BudoShurenWebsite.Tests.Unit.Mail;

[Trait("Category", "Unit")]
public class EmailWiederholungTests
{
    private static readonly DateTime Jetzt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(10, 60)]
    public void Wartezeit_steigt_mit_der_Zahl_der_Versuche(int versuche, int erwarteteMinuten)
    {
        EmailWiederholung.Wartezeit(versuche).ShouldBe(TimeSpan.FromMinutes(erwarteteMinuten));
    }

    [Fact]
    public void Fehlschlag_unter_der_Grenze_plant_neuen_Versuch()
    {
        var mail = new EmailAusgang { Versuche = 1, FaelligAbUtc = Jetzt.AddHours(-1) };

        EmailWiederholung.FehlschlagVermerken(mail, "Zeitüberschreitung", Jetzt, maxVersuche: 5);

        mail.Versuche.ShouldBe(2);
        mail.Status.ShouldBe(EmailStatus.Wartend);
        mail.FaelligAbUtc.ShouldBe(Jetzt.AddMinutes(5));
        mail.LetzterFehler.ShouldBe("Zeitüberschreitung");
    }

    [Fact]
    public void Fehlschlag_beim_letzten_Versuch_ist_endgueltig()
    {
        var mail = new EmailAusgang { Versuche = 4, FaelligAbUtc = Jetzt };

        EmailWiederholung.FehlschlagVermerken(mail, "Postfach existiert nicht", Jetzt, maxVersuche: 5);

        mail.Versuche.ShouldBe(5);
        mail.Status.ShouldBe(EmailStatus.Fehlgeschlagen);
        mail.FaelligAbUtc.ShouldBe(Jetzt, "bei endgültigem Fehlschlag wird nichts mehr geplant");
    }

    [Fact]
    public void Langer_Fehlertext_wird_gekuerzt()
    {
        var mail = new EmailAusgang();

        EmailWiederholung.FehlschlagVermerken(mail, new string('x', 5000), Jetzt, maxVersuche: 5);

        mail.LetzterFehler!.Length.ShouldBe(EmailWiederholung.MaxFehlerLaenge);
    }
}
