using BudoShurenWebsite.Services.Systemzustand;

namespace BudoShurenWebsite.Services.Kontakt
{
    /// <summary>
    /// Dünne Hülle um KontaktAufraeumJob: beim Start und danach stündlich.
    /// Abschaltbar mit Kontaktformular:AufraeumenAktiviert=false (Tests).
    /// </summary>
    public sealed class KontaktAufraeumHostedService : BackgroundService
    {
        private static readonly TimeSpan Intervall = TimeSpan.FromHours(1);

        private readonly KontaktAufraeumJob _job;
        private readonly TimeProvider _zeit;
        private readonly bool _aktiviert;
        private readonly DienstHerzschlag _herzschlag;
        private readonly ILogger<KontaktAufraeumHostedService> _logger;

        public KontaktAufraeumHostedService(KontaktAufraeumJob job, TimeProvider zeit, IConfiguration konfiguration, DienstHerzschlag herzschlag, ILogger<KontaktAufraeumHostedService> logger)
        {
            _job = job;
            _zeit = zeit;
            _aktiviert = konfiguration.GetValue("Kontaktformular:AufraeumenAktiviert", true);
            _herzschlag = herzschlag;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_aktiviert)
            {
                _logger.LogInformation("Aufräumen des Kontaktformular-Protokolls ist deaktiviert (Kontaktformular:AufraeumenAktiviert)");
                _herzschlag.Abgeschaltet(DienstHerzschlag.KontaktAufraeumen, "Kontaktformular:AufraeumenAktiviert=false", erwartet: false);
                return;
            }

            _herzschlag.Gestartet(DienstHerzschlag.KontaktAufraeumen, Intervall + TimeSpan.FromHours(1));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var geleert = await _job.AusfuehrenAsync(stoppingToken);
                    if (geleert > 0)
                        _logger.LogInformation("Absender und Nachricht von {Anzahl} Einträgen des Kontaktformular-Protokolls gelöscht", geleert);
                    _herzschlag.Gelaufen(DienstHerzschlag.KontaktAufraeumen, ok: true);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Z. B. Datenbank kurz nicht erreichbar: der nächste Durchlauf versucht es erneut
                    _logger.LogError(ex, "Fehler beim Aufräumen des Kontaktformular-Protokolls");
                    _herzschlag.Gelaufen(DienstHerzschlag.KontaktAufraeumen, ok: false);
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
