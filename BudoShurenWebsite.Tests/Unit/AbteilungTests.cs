using BudoShurenWebsite.Models;

namespace BudoShurenWebsite.Tests.Unit;

[Trait("Category", "Unit")]
public class AbteilungTests
{
    // Produktion: Id und Name sind gleich; die Entwicklungsdatenbank hat dagegen Zahlen als Id
    private static readonly Abteilung MitZahlId = new() { ID = "1", Name = "Aikido" };

    [Theory]
    [InlineData("1")]
    [InlineData("Aikido")]
    [InlineData("aikido")]
    public void Passt_zu_Id_oder_Name(string wert)
    {
        MitZahlId.Passt(wert).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Bujinkan")]
    [InlineData("2")]
    public void Passt_nicht_zu_leeren_oder_fremden_Werten(string? wert)
    {
        MitZahlId.Passt(wert).ShouldBeFalse();
    }

    [Fact]
    public void Leerer_Wert_passt_auch_nicht_zu_Abteilung_ohne_Name()
    {
        new Abteilung { ID = "1" }.Passt(null).ShouldBeFalse();
    }
}
