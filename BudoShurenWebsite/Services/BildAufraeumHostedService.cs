namespace BudoShurenWebsite.Services
{
    /// <summary>
    /// Dünne Hülle um BildAufraeumJob: beim Start und danach stündlich. Unabhängig vom Modul Veranstaltungen,
    /// weil auch Aktuelles vorläufige Bilder hochlädt. Abschaltbar mit BildAufraeumen:Aktiviert=false (Tests).
    /// </summary>
    public sealed class BildAufraeumHostedService : BackgroundService
    {
        private static readonly TimeSpan Intervall = TimeSpan.FromHours(1);

        private readonly BildAufraeumJob _job;
        private readonly TimeProvider _zeit;
        private readonly bool _aktiviert;
        private readonly ILogger<BildAufraeumHostedService> _logger;

        public BildAufraeumHostedService(BildAufraeumJob job, TimeProvider zeit, IConfiguration konfiguration, ILogger<BildAufraeumHostedService> logger)
        {
            _job = job;
            _zeit = zeit;
            _aktiviert = konfiguration.GetValue("BildAufraeumen:Aktiviert", true);
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_aktiviert)
            {
                _logger.LogInformation("Aufräumen nicht gespeicherter Bilder ist deaktiviert (BildAufraeumen:Aktiviert)");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var geloescht = await _job.AusfuehrenAsync(stoppingToken);
                    if (geloescht > 0)
                        _logger.LogInformation("{Anzahl} nie gespeicherte Bilder gelöscht", geloescht);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Z. B. Datenbank kurz nicht erreichbar: der nächste Durchlauf versucht es erneut
                    _logger.LogError(ex, "Fehler beim Aufräumen nicht gespeicherter Bilder");
                }

                try
                {
                    await Task.Delay(Intervall, _zeit, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
