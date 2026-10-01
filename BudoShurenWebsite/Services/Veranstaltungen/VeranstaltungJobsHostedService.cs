using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Dünne Hülle um BenachrichtigungJob (jede Minute) und VeranstaltungWartungJob (stündlich, erstmals beim Start).
    /// Läuft nur, wenn das Modul eingeschaltet ist. Was z. B. während eines IIS-Neustarts liegen geblieben ist,
    /// holt der erste Durchlauf nach (BenachrichtigtBisUtc bzw. Fristen stehen in der Datenbank).
    /// </summary>
    public sealed class VeranstaltungJobsHostedService : BackgroundService
    {
        private static readonly TimeSpan Takt = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan WartungIntervall = TimeSpan.FromHours(1);

        private readonly BenachrichtigungJob _benachrichtigung;
        private readonly VeranstaltungWartungJob _wartung;
        private readonly TimeProvider _zeit;
        private readonly VeranstaltungenOptionen _optionen;
        private readonly ILogger<VeranstaltungJobsHostedService> _logger;

        public VeranstaltungJobsHostedService(
            BenachrichtigungJob benachrichtigung,
            VeranstaltungWartungJob wartung,
            TimeProvider zeit,
            IOptions<VeranstaltungenOptionen> optionen,
            ILogger<VeranstaltungJobsHostedService> logger)
        {
            _benachrichtigung = benachrichtigung;
            _wartung = wartung;
            _zeit = zeit;
            _optionen = optionen.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_optionen.Aktiviert || !_optionen.HintergrundJobsAktiviert)
            {
                _logger.LogInformation("Hintergrund-Jobs des Moduls Veranstaltungen laufen nicht (Aktiviert={Aktiviert}, HintergrundJobsAktiviert={Jobs})",
                    _optionen.Aktiviert, _optionen.HintergrundJobsAktiviert);
                return;
            }

            var naechsteWartung = DateTimeOffset.MinValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_zeit.GetUtcNow() >= naechsteWartung)
                {
                    await SicherAusfuehrenAsync("Wartung", async () =>
                    {
                        var ergebnis = await _wartung.AusfuehrenAsync(stoppingToken);
                        if (ergebnis.VerworfeneAnmeldungen > 0 || ergebnis.AbgeschlosseneVeranstaltungen > 0)
                            _logger.LogInformation("Wartung Veranstaltungen: {Anmeldungen} abgelaufene Anmeldungen verworfen, {Veranstaltungen} Veranstaltungen abgeschlossen",
                                ergebnis.VerworfeneAnmeldungen, ergebnis.AbgeschlosseneVeranstaltungen);
                    }, stoppingToken);
                    naechsteWartung = _zeit.GetUtcNow() + WartungIntervall;
                }

                await SicherAusfuehrenAsync("Benachrichtigungen", () => _benachrichtigung.AusfuehrenAsync(stoppingToken), stoppingToken);

                try
                {
                    await Task.Delay(Takt, _zeit, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // Ein Fehler in einem Durchlauf (z. B. Datenbank kurz nicht erreichbar) beendet den Dienst nicht; der nächste Takt versucht es erneut
        private async Task SicherAusfuehrenAsync(string name, Func<Task> aktion, CancellationToken stoppingToken)
        {
            try
            {
                await aktion();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Job {Job} des Moduls Veranstaltungen", name);
            }
        }
    }
}
