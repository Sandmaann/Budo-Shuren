using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Tests.Unit.Datenbank;

[Trait("Category", "Unit")]
public class ModellTests
{
    [Fact]
    public void Modell_hat_keine_Aenderungen_ohne_Migration()
    {
        // Es wird keine Verbindung aufgebaut, verglichen wird nur das Modell mit dem Migrations-Snapshot
        var optionen = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=nicht-verwendet;Database=Modellpruefung")
            .Options;
        using var kontext = new ApplicationDbContext(optionen);

        kontext.Database.HasPendingModelChanges().ShouldBeFalse(
            "Das Modell wurde geändert, ohne eine Migration anzulegen (dotnet ef migrations add <Name>).");
    }
}
