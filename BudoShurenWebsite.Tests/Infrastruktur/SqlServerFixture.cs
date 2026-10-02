using BudoShurenWebsite.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Respawn;
using Respawn.Graph;
using Testcontainers.MsSql;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Stellt eine migrierte Testdatenbank bereit (BUDO_TEST_SQL oder SQL-Server-Container)
/// und setzt sie zwischen den Tests per Respawn zurück.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;
    private Respawner? _respawner;

    public string Verbindung { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        if (!TestDatenbank.Verfuegbar)
            return;

        var ausUmgebung = TestDatenbank.VerbindungAusUmgebung;
        if (ausUmgebung is not null)
        {
            PruefeTestdatenbank(ausUmgebung);
            Verbindung = ausUmgebung;
        }
        else
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            Verbindung = new SqlConnectionStringBuilder(_container.GetConnectionString())
            {
                InitialCatalog = "BudoShurenTests"
            }.ConnectionString;
        }

        await using (var kontext = NeuerKontext())
        {
            await kontext.Database.MigrateAsync();
        }

        await DataProtectionTabelleAnlegenAsync();

        await using var verbindung = new SqlConnection(Verbindung);
        await verbindung.OpenAsync();
        _respawner = await Respawner.CreateAsync(verbindung, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToIgnore = [new Table("__EFMigrationsHistory")]
        });
    }

    public ApplicationDbContext NeuerKontext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(Verbindung).Options);

    /// <summary>Löscht alle Daten außer der Migrationshistorie.</summary>
    public async Task ZuruecksetzenAsync()
    {
        if (_respawner is null)
            return;

        await using var verbindung = new SqlConnection(Verbindung);
        await verbindung.OpenAsync();
        await _respawner.ResetAsync(verbindung);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    /// <summary>
    /// Auf einer frischen Datenbank fehlt die Tabelle DataProtectionKeys, bis die App sie beim Start
    /// anlegt. Tests ohne App-Start brauchen sie trotzdem, deshalb wird sie hier angelegt.
    /// </summary>
    private async Task DataProtectionTabelleAnlegenAsync()
    {
        var optionen = new DbContextOptionsBuilder<DataProtectionKeyContext>().UseSqlServer(Verbindung).Options;
        await using var kontext = new DataProtectionKeyContext(optionen);

        // Bei einer wiederverwendeten LocalDB-Datenbank existiert die Tabelle schon
        var vorhanden = await kontext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sys.tables WHERE name = 'DataProtectionKeys'")
            .SingleAsync() > 0;

        if (!vorhanden)
            await kontext.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    /// <summary>Schutz davor, dass BUDO_TEST_SQL versehentlich auf eine echte Datenbank zeigt (Respawn löscht alles).</summary>
    private static void PruefeTestdatenbank(string verbindung)
    {
        var datenbank = new SqlConnectionStringBuilder(verbindung).InitialCatalog;
        if (!datenbank.Contains("Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{TestDatenbank.UmgebungsVariable} muss auf eine Testdatenbank zeigen (Name enthält \"Test\"), nicht auf \"{datenbank}\". " +
                "Die Tests löschen zwischen den Läufen alle Daten.");
        }
    }
}
