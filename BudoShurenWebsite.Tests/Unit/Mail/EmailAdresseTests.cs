using BudoShurenWebsite.Services.Mail;

namespace BudoShurenWebsite.Tests.Unit.Mail;

[Trait("Category", "Unit")]
public class EmailAdresseTests
{
    [Theory]
    [InlineData("max@example.org", true)]
    [InlineData("  max.muster+seminar@example.org ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("max", false)]
    [InlineData("Max <max@example.org>", false)]
    [InlineData("a@example.org, b@example.org", false)]
    public void IstGueltig(string? adresse, bool gueltig)
    {
        EmailAdresse.IstGueltig(adresse).ShouldBe(gueltig);
    }

    [Fact]
    public void Normalisieren_entfernt_Leerzeichen_und_schreibt_klein()
    {
        EmailAdresse.Normalisieren("  Max.Muster@Example.ORG ").ShouldBe("max.muster@example.org");
    }
}
