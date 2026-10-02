using BudoShurenWebsite.Services.Systemzustand;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>
    /// Dünne Hülle um EmailVersandJob: versendet fällige Mails, sobald angestoßen oder spätestens
    /// nach dem Abfrageintervall, und räumt einmal täglich alte Einträge auf.
    /// Holt beim Start alles nach, was z. B. während eines IIS-Neustarts liegen geblieben ist.
    /// </summary>
    public sealed class EmailVersandHostedService : BackgroundService
    {
        private static readonly TimeSpan BereinigungsIntervall = TimeSpan.FromHours(24);

        private readonly EmailVersandJob _job;
        private readonly EmailVersandSignal _signal;
        private readonly TimeProvider _zeit;
        private readonly EmailVersandOptionen _optionen;
        private readonly DienstHerzschlag _herzschlag;
        private readonly ILogger<EmailVersandHostedService> _logger;

        public EmailVersandHostedService(
            EmailVersandJob job,
            EmailVersandSignal signal,
            TimeProvider zeit,
            IOptions<EmailVersandOptionen> optionen,
            DienstHerzschlag herzschlag,
            ILogger<EmailVersandHostedService> logger)
        {
            _job = job;
            _signal = signal;
            _zeit = zeit;
            _optionen = optionen.Value;
            _herzschlag = herzschlag;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_optionen.Aktiviert)
            {
                _logger.LogInformation("Mailversand aus der Warteschlange ist deaktiviert ({Abschnitt}:Aktiviert)", EmailVersandOptionen.Abschnitt);
                _herzschlag.Abgeschaltet(DienstHerzschlag.EmailVersand, $"{EmailVersandOptionen.Abschnitt}:Aktiviert=false", erwartet: false);
                return;
            }

            // Ein Durchlauf wartet höchstens Abfrageintervall bzw. nach einem Fehler WartezeitNachFehler;
            // der Rest ist Puffer für große Stapel und langsame SMTP-Antworten
            _herzschlag.Gestartet(DienstHerzschlag.EmailVersand, _optionen.Abfrageintervall + _optionen.WartezeitNachFehler + TimeSpan.FromMinutes(30));

            var naechsteBereinigung = DateTimeOffset.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_zeit.GetUtcNow() >= naechsteBereinigung)
                    {
                        var geloescht = await _job.AlteEintraegeLoeschenAsync(stoppingToken);
                        if (geloescht > 0)
                            _logger.LogInformation("{Anzahl} alte Einträge aus der Mail-Warteschlange gelöscht", geloescht);
                        naechsteBereinigung = _zeit.GetUtcNow() + BereinigungsIntervall;
                    }

                    // Volle Stapel direkt nacheinander abarbeiten, danach auf Anstoß oder Intervall warten
                    while (await _job.StapelVersendenAsync(stoppingToken) >= _optionen.StapelGroesse)
                    {
                    }
                    _herzschlag.Gelaufen(DienstHerzschlag.EmailVersand, ok: true);

                    await _signal.WartenAsync(_optionen.Abfrageintervall, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fehler beim Mailversand aus der Warteschlange, neuer Versuch in {Wartezeit}", _optionen.WartezeitNachFehler);
                    _herzschlag.Gelaufen(DienstHerzschlag.EmailVersand, ok: false);
                    try
                    {
                        await Task.Delay(_optionen.WartezeitNachFehler, _zeit, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }
}
