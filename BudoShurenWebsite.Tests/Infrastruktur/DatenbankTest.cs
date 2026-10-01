namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>Alle Tests mit Datenbank laufen nacheinander in einer Collection und teilen sich die Fixture.</summary>
[CollectionDefinition(Name)]
public sealed class DatenbankCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "Datenbank";
}

/// <summary>
/// Basisklasse für Integrations- und HTTP-Tests: setzt vor jedem Test die Datenbank zurück.
/// Testmethoden verwenden [DatenbankFact] und tragen [Trait("Category", "Integration")].
/// </summary>
[Collection(DatenbankCollection.Name)]
public abstract class DatenbankTest(SqlServerFixture datenbank) : IAsyncLifetime
{
    protected SqlServerFixture Datenbank { get; } = datenbank;

    public virtual async ValueTask InitializeAsync()
    {
        if (TestDatenbank.Verfuegbar)
            await Datenbank.ZuruecksetzenAsync();
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
