namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Ermittelt, ob für Integrationstests ein SQL Server zur Verfügung steht:
/// entweder über die Umgebungsvariable BUDO_TEST_SQL (z. B. LocalDB) oder über Docker (Testcontainers).
/// </summary>
public static class TestDatenbank
{
    public const string UmgebungsVariable = "BUDO_TEST_SQL";

    public const string Hinweis =
        "Kein SQL Server für Integrationstests: BUDO_TEST_SQL setzen (z. B. LocalDB) oder Docker starten.";

    public static string? VerbindungAusUmgebung =>
        Environment.GetEnvironmentVariable(UmgebungsVariable) is { Length: > 0 } verbindung ? verbindung : null;

    public static bool Verfuegbar => VerbindungAusUmgebung is not null || DockerVorhanden.Value;

    // Wird pro Testlauf nur einmal ermittelt (unter Windows werden dafür die Named Pipes durchsucht)
    private static readonly Lazy<bool> DockerVorhanden = new(ErmittleDocker);

    private static bool ErmittleDocker()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOCKER_HOST")))
            return true;

        if (File.Exists("/var/run/docker.sock"))
            return true;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                return Directory.GetFiles(@"\\.\pipe\").Any(p => p.EndsWith("docker_engine", StringComparison.OrdinalIgnoreCase));
            }
            catch (IOException)
            {
                return false;
            }
        }

        return false;
    }
}
