using BetterStack.Logs.NLog;
using NLog;
using NLog.Layouts;

namespace BudoShurenWebsite.Global
{
    /// <summary>
    /// Hängt das BetterStack-Ziel an die NLog-Konfiguration. Token und Endpoint stehen nicht in der nlog.config,
    /// sondern im Abschnitt "BetterStack" der Konfiguration (User Secrets bzw. appsettings.json auf dem Server).
    /// </summary>
    public static class BetterStackProtokoll
    {
        public const string Abschnitt = "BetterStack";
        public const string ZielName = "betterstack";

        private const string ZeilenLayout = "${uppercase:${level}}|${logger}|${aspnet-traceidentifier}|${message}${onexception:inner=|${exception:format=tostring}}";

        /// <summary>
        /// Richtet das Ziel ein, wenn Token und Endpoint gesetzt sind. Liefert false, wenn einer der Werte fehlt;
        /// dann wird nur auf Konsole und in die Datei geloggt.
        /// </summary>
        public static bool Einrichten(LogFactory fabrik, IConfiguration konfiguration)
        {
            var token = konfiguration[$"{Abschnitt}:SourceToken"];
            var endpoint = konfiguration[$"{Abschnitt}:Endpoint"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(endpoint))
            {
                return false;
            }

            Anhaengen(fabrik, token, endpoint);
            // Die nlog.config wird bei Änderungen neu geladen (autoReload), das Ziel wäre danach weg.
            fabrik.ConfigurationChanged += (_, _) => Anhaengen(fabrik, token, endpoint);
            return true;
        }

        private static void Anhaengen(LogFactory fabrik, string token, string endpoint)
        {
            var konfiguration = fabrik.Configuration;
            if (konfiguration == null || konfiguration.FindTargetByName(ZielName) != null)
            {
                return;
            }

            var ziel = new BetterStackLogsTarget
            {
                Name = ZielName,
                Layout = ZeilenLayout,
                SourceToken = Layout.FromLiteral(token),
                Endpoint = Layout.FromLiteral(endpoint),
            };
            konfiguration.AddTarget(ziel);
            // Wie die letzte Regel der nlog.config: steht hinter den Regeln, die Framework-Meldungen verwerfen.
            konfiguration.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, ziel, "*");
            fabrik.ReconfigExistingLoggers();
        }
    }
}
