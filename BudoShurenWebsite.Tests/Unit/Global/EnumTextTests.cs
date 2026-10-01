using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Tests.Unit.Global;

[Trait("Category", "Unit")]
public class EnumTextTests
{
    [Fact]
    public void Liefert_den_Text_aus_Description()
    {
        VeranstaltungStatus.Veroeffentlicht.Beschreibung().ShouldBe("Veröffentlicht");
    }

    [Fact]
    public void Ohne_Description_den_Namen()
    {
        BenachrichtigungEreignisse.Keine.Beschreibung().ShouldBe("Keine");
    }
}
