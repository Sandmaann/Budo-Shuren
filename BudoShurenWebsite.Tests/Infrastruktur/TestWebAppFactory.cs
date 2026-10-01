using BudoShurenWebsite.Services.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Startet die komplette App in der Umgebung "Test" gegen die Testdatenbank.
/// Mails gehen nie an einen echten SMTP-Server, sondern an <see cref="Mails"/>.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    public FakeMailTransport Mails { get; } = new();

    public TestWebAppFactory(string verbindung)
    {
        // Program.Main liest Connection String und NLog-Konfiguration, bevor ConfigureWebHost greift.
        // Deshalb über Umgebungsvariablen setzen.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", verbindung);
        // Kein Hintergrundversand: Tests rufen EmailVersandJob bei Bedarf direkt auf
        Environment.SetEnvironmentVariable("EmailVersand__Aktiviert", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureTestServices(dienste =>
            dienste.Replace(ServiceDescriptor.Singleton<IMailTransport>(Mails)));
    }
}
