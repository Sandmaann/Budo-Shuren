using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Startet die komplette App in der Umgebung "Test" gegen die Testdatenbank.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    public TestWebAppFactory(string verbindung)
    {
        // Program.Main liest Connection String und NLog-Konfiguration, bevor ConfigureWebHost greift.
        // Deshalb über Umgebungsvariablen setzen.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", verbindung);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
    }
}
