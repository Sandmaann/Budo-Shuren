using BudoShurenWebsite.Services.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Startet die komplette App in der Umgebung "Test" gegen die Testdatenbank.
/// Mails gehen nie an einen echten SMTP-Server, sondern an <see cref="Mails"/>.
/// </summary>
public sealed class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly TimeProvider? _zeit;

    public FakeMailTransport Mails { get; } = new();

    /// <param name="veranstaltungenAktiviert">Feature-Schalter Veranstaltungen:Aktiviert.</param>
    /// <param name="zeit">Ersetzt die Uhr der App (z. B. FakeTimeProvider); sonst die echte Zeit.</param>
    public TestWebAppFactory(string verbindung, bool veranstaltungenAktiviert = true, FakeTimeProvider? zeit = null)
    {
        _zeit = zeit;

        // Program.Main liest Connection String und NLog-Konfiguration, bevor ConfigureWebHost greift.
        // Deshalb über Umgebungsvariablen setzen (die Datenbanktests laufen nacheinander).
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", verbindung);
        // Kein Hintergrundversand: Tests rufen EmailVersandJob bei Bedarf direkt auf
        Environment.SetEnvironmentVariable("EmailVersand__Aktiviert", "false");
        Environment.SetEnvironmentVariable("Veranstaltungen__Aktiviert", veranstaltungenAktiviert ? "true" : "false");
        // Benachrichtigungs- und Wartungs-Job ebenso: Tests rufen sie direkt auf
        Environment.SetEnvironmentVariable("Veranstaltungen__HintergrundJobsAktiviert", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureTestServices(dienste =>
        {
            dienste.Replace(ServiceDescriptor.Singleton<IMailTransport>(Mails));
            TestAnmeldung.Registrieren(dienste);
            if (_zeit is not null)
                dienste.Replace(ServiceDescriptor.Singleton(typeof(TimeProvider), _zeit));
        });
    }
}
