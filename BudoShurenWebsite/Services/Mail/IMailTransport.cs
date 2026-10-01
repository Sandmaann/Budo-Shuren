using MimeKit;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>
    /// Kapselt den SMTP-Versand. In Produktion MailKitTransport, in Tests ein Fake.
    /// </summary>
    public interface IMailTransport
    {
        /// <summary>Öffnet eine Verbindung, über die mehrere Mails nacheinander versendet werden können.</summary>
        Task<IMailVerbindung> VerbindenAsync(CancellationToken abbruch);
    }

    public interface IMailVerbindung : IAsyncDisposable
    {
        /// <summary>Versendet eine Mail. Ist kein Absender gesetzt, wird die System-Adresse eingetragen.</summary>
        Task SendenAsync(MimeMessage nachricht, CancellationToken abbruch);
    }
}
