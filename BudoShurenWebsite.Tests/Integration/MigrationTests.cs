using BudoShurenWebsite.Data;
using BudoShurenWebsite.Tests.Infrastruktur;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Tests.Integration;

[Trait("Category", "Integration")]
public class MigrationTests(SqlServerFixture datenbank) : DatenbankTest(datenbank)
{
    [DatenbankFact]
    public async Task Alle_Migrationen_laufen_auf_einer_leeren_Datenbank_durch()
    {
        // Eigene, frische Datenbank auf demselben Server: so wird wirklich von null an migriert
        var verbindung = new SqlConnectionStringBuilder(Datenbank.Verbindung);
        verbindung.InitialCatalog += "_Migration";
        var optionen = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(verbindung.ConnectionString)
            .Options;
        var abbruch = TestContext.Current.CancellationToken;

        await using var kontext = new ApplicationDbContext(optionen);
        await kontext.Database.EnsureDeletedAsync(abbruch);
        try
        {
            await kontext.Database.MigrateAsync(abbruch);

            (await kontext.Database.GetPendingMigrationsAsync(abbruch)).ShouldBeEmpty();
            (await kontext.Database.GetAppliedMigrationsAsync(abbruch))
                .ShouldBe(kontext.Database.GetMigrations());
        }
        finally
        {
            await kontext.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}
