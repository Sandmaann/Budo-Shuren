using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class TagBelegungTests
{
    [Theory]
    [InlineData(20, 5, 15)]
    [InlineData(20, 20, 0)]
    [InlineData(10, 12, 0)]
    [InlineData(null, 7, null)]
    public void Freie_Plaetze_nie_negativ_und_null_bei_unbegrenzt(int? max, int belegt, int? frei)
    {
        var tag = new TagBelegung(3, new DateOnly(2026, 11, 14), new TimeOnly(10, 0), new TimeOnly(16, 0), "Kata", false, max, belegt);

        tag.AlsAnzeige().ShouldBe(new TagAnzeige(3, tag.Datum, tag.Beginn, tag.Ende, "Kata", false, frei));
    }
}
