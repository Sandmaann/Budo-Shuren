using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>
    /// Versendet fällige Mails aus der Warteschlange. Enthält die eigentliche Logik,
    /// damit Tests sie ohne den EmailVersandHostedService direkt aufrufen können.
    /// Darf nur von einer Instanz gleichzeitig ausgeführt werden (die App läuft als einzelne Instanz).
    /// </summary>
    public sealed class EmailVersandJob
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly IMailTransport _transport;
        private readonly TimeProvider _zeit;
        private readonly EmailVersandOptionen _optionen;
        private readonly ILogger<EmailVersandJob> _logger;

        public EmailVersandJob(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            IMailTransport transport,
            TimeProvider zeit,
            IOptions<EmailVersandOptionen> optionen,
            ILogger<EmailVersandJob> logger)
        {
            _dbFactory = dbFactory;
            _transport = transport;
            _zeit = zeit;
            _optionen = optionen.Value;
            _logger = logger;
        }

        /// <summary>
        /// Versendet höchstens einen Stapel fälliger Mails über eine SMTP-Verbindung.
        /// Gibt die Anzahl bearbeiteter Mails zurück (versendet oder Fehlversuch vermerkt).
        /// Ist der SMTP-Server nicht erreichbar, wird die Exception weitergereicht und nichts verändert.
        /// </summary>
        public async Task<int> StapelVersendenAsync(CancellationToken abbruch)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var jetzt = _zeit.GetUtcNow().UtcDateTime;

            var faellige = await kontext.EmailAusgang
                .Where(m => m.Status == EmailStatus.Wartend && m.FaelligAbUtc <= jetzt)
                .OrderBy(m => m.Prioritaet)
                .ThenBy(m => m.FaelligAbUtc)
                .ThenBy(m => m.Id)
                .Take(_optionen.StapelGroesse)
                .ToListAsync(abbruch);

            if (faellige.Count == 0)
                return 0;

            await using var verbindung = await _transport.VerbindenAsync(abbruch);

            foreach (var mail in faellige)
            {
                abbruch.ThrowIfCancellationRequested();

                try
                {
                    // Nicht mitten im Versand abbrechen: sonst wäre die Mail evtl. raus, aber nicht als gesendet gespeichert
                    await verbindung.SendenAsync(EmailNachrichtFabrik.Erstellen(mail), CancellationToken.None);
                    mail.Status = EmailStatus.Gesendet;
                    mail.GesendetAmUtc = _zeit.GetUtcNow().UtcDateTime;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    EmailWiederholung.FehlschlagVermerken(mail, ex.Message, _zeit.GetUtcNow().UtcDateTime, _optionen.MaxVersuche);
                    if (mail.Status == EmailStatus.Fehlgeschlagen)
                        _logger.LogError(ex, "Mail {MailId} endgültig nicht versendet nach {Versuche} Versuchen", mail.Id, mail.Versuche);
                    else
                        _logger.LogWarning(ex, "Mail {MailId} nicht versendet (Versuch {Versuche}), neuer Versuch ab {FaelligAb:u}", mail.Id, mail.Versuche, mail.FaelligAbUtc);
                }

                // Nach jeder Mail speichern, ohne Abbruch: eine versendete Mail darf nicht erneut versendet werden
                await kontext.SaveChangesAsync(CancellationToken.None);

                if (_optionen.PauseZwischenMails > TimeSpan.Zero)
                    await Task.Delay(_optionen.PauseZwischenMails, _zeit, abbruch);
            }

            return faellige.Count;
        }

        /// <summary>Löscht versendete und endgültig fehlgeschlagene Einträge nach Ablauf der Aufbewahrungsfrist.</summary>
        public async Task<int> AlteEintraegeLoeschenAsync(CancellationToken abbruch)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var grenze = _zeit.GetUtcNow().UtcDateTime.AddDays(-_optionen.AufbewahrungTage);

            return await kontext.EmailAusgang
                .Where(m => m.Status != EmailStatus.Wartend && m.ErstelltUtc < grenze)
                .ExecuteDeleteAsync(abbruch);
        }
    }
}
