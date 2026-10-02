using BudoShurenWebsite.Services.Systemzustand;
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
        private readonly DienstHerzschlag _herzschlag;
        private readonly ILogger<VeranstaltungJobsHostedService> _logger;

        public VeranstaltungJobsHostedService(
            BenachrichtigungJob benachrichtigung,
            VeranstaltungWartungJob wartung,
            TimeProvider zeit,
            IOptions<VeranstaltungenOptionen> optionen,
            DienstHerzschlag herzschlag,
            ILogger<VeranstaltungJobsHostedService> logger)
        {
            _benachrichtigung = benachrichtigung;
            _wartung = wartung;
            _zeit = zeit;
            _optionen = optionen.Value;
            _herzschlag = herzschlag;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_optionen.Aktiviert || !_optionen.HintergrundJobsAktiviert)
            {
                _logger.LogInformation("Hintergrund-Jobs des Moduls Veranstaltungen laufen nicht (Aktiviert={Aktiviert}, HintergrundJobsAktiviert={Jobs})",
                    _optionen.Aktiviert, _optionen.HintergrundJobsAktiviert);
                // Modul aus: gewollt. Modul an, Jobs aus: Benachrichtigungen und Wartung bleiben liegen
                _herzschlag.Abgeschaltet(DienstHerzschlag.Veranstaltungen,
                    _optionen.Aktiviert ? "Veranstaltungen:HintergrundJobsAktiviert=false" : "Modul Veranstaltungen ausgeschaltet",
                    erwartet: !_optionen.Aktiviert);
                return;
            }

            _herzschlag.Gestartet(DienstHerzschlag.Veranstaltungen, Takt + TimeSpan.FromMinutes(15));

            var naechsteWartung = DateTimeOffset.MinValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                var ok = true;
                if (_zeit.GetUtcNow() >= naechsteWartung)
                {
                    ok &= await SicherAusfuehrenAsync("Wartung", async () =>
                    {
                        var ergebnis = await _wartung.AusfuehrenAsync(stoppingToken);
                        if (ergebnis.VerworfeneAnmeldungen > 0 || ergebnis.AbgeschlosseneVeranstaltungen > 0)
                            _logger.LogInformation("Wartung Veranstaltungen: {Anmeldungen} abgelaufene Anmeldungen verworfen, {Veranstaltungen} Veranstaltungen abgeschlossen",
                                ergebnis.VerworfeneAnmeldungen, ergebnis.AbgeschlosseneVeranstaltungen);
                    }, stoppingToken);
                    naechsteWartung = _zeit.GetUtcNow() + WartungIntervall;
                }

                ok &= await SicherAusfuehrenAsync("Benachrichtigungen", () => _benachrichtigung.AusfuehrenAsync(stoppingToken), stoppingToken);
                // Ein Fehler der stündlichen Wartung zeigt sich hier nur bis zum nächsten Takt (steht im Log);
                // liegengebliebene Wartung meldet zusätzlich VeranstaltungWartungCheck
                _herzschlag.Gelaufen(DienstHerzschlag.Veranstaltungen, ok);

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
        /// <returns>false, wenn der Durchlauf mit einem Fehler endete.</returns>
        private async Task<bool> SicherAusfuehrenAsync(string name, Func<Task> aktion, CancellationToken stoppingToken)
        {
            try
            {
                await aktion();
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler im Job {Job} des Moduls Veranstaltungen", name);
                return false;
            }
        }
    }
}
