using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>IDbContextFactory für Services unter Test, liefert Kontexte auf die Testdatenbank.</summary>
public sealed class TestKontextFabrik(SqlServerFixture datenbank) : IDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext() => datenbank.NeuerKontext();
}
