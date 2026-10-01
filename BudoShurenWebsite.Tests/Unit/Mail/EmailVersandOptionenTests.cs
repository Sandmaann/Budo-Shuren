using BudoShurenWebsite.Services.Mail;

namespace BudoShurenWebsite.Tests.Unit.Mail;

[Trait("Category", "Unit")]
public class EmailVersandOptionenTests
{
    [Fact]
    public void Standardwerte_sind_gueltig()
    {
        new EmailVersandOptionen().IstGueltig().ShouldBeTrue();
    }

    [Fact]
    public void Stapelgroesse_0_ist_ungueltig()
    {
        new EmailVersandOptionen { StapelGroesse = 0 }.IstGueltig().ShouldBeFalse();
    }

    [Fact]
    public void Abfrageintervall_0_ist_ungueltig()
    {
        new EmailVersandOptionen { Abfrageintervall = TimeSpan.Zero }.IstGueltig().ShouldBeFalse();
    }
}
