using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>
    /// SMTP-Versand über MailKit mit den Zugangsdaten aus der Tabelle EmailSettings (Eintrag mit IsMain).
    /// </summary>
    public sealed class MailKitTransport : IMailTransport
    {
        public const string AbsenderName = "Budo Shuren Dojo";

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

        public MailKitTransport(IDbContextFactory<ApplicationDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<IMailVerbindung> VerbindenAsync(CancellationToken abbruch)
        {
            var einstellungen = await LadeEinstellungenAsync(abbruch);

            var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(einstellungen.SmtpServer, einstellungen.SmtpPort, SecureSocketOptions.StartTls, abbruch);
                await client.AuthenticateAsync(einstellungen.SmtpUser, einstellungen.SmtpPassword, abbruch);
            }
            catch
            {
                client.Dispose();
                throw;
            }

            return new Verbindung(client, new MailboxAddress(AbsenderName, einstellungen.SmtpUser));
        }

        private async Task<EmailSetting> LadeEinstellungenAsync(CancellationToken abbruch)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var einstellungen = await kontext.EmailSettings.AsNoTracking().FirstOrDefaultAsync(x => x.IsMain, abbruch)
                ?? throw new InvalidOperationException("Keine E-Mail-Einstellungen (IsMain) in der Datenbank gefunden.");

            if (string.IsNullOrWhiteSpace(einstellungen.SmtpServer))
                throw new InvalidOperationException("In den E-Mail-Einstellungen fehlt der SMTP-Server.");
            if (string.IsNullOrWhiteSpace(einstellungen.SmtpUser))
                throw new InvalidOperationException("In den E-Mail-Einstellungen fehlt der SMTP-Benutzer.");

            return einstellungen;
        }

        private sealed class Verbindung : IMailVerbindung
        {
            private readonly SmtpClient _client;
            private readonly MailboxAddress _absender;

            public Verbindung(SmtpClient client, MailboxAddress absender)
            {
                _client = client;
                _absender = absender;
            }

            public async Task SendenAsync(MimeMessage nachricht, CancellationToken abbruch)
            {
                if (nachricht.From.Count == 0)
                    nachricht.From.Add(_absender);

                await _client.SendAsync(nachricht, abbruch);
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    if (_client.IsConnected)
                        await _client.DisconnectAsync(true);
                }
                finally
                {
                    _client.Dispose();
                }
            }
        }
    }
}
