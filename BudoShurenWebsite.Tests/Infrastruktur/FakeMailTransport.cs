using BudoShurenWebsite.Services.Mail;
using MimeKit;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Ersetzt den SMTP-Versand in Tests: merkt sich versendete Mails und kann Fehler simulieren.
/// </summary>
public sealed class FakeMailTransport : IMailTransport
{
    private readonly List<MimeMessage> _gesendet = [];

    public IReadOnlyList<MimeMessage> Gesendet => _gesendet;

    public int Verbindungen { get; private set; }

    /// <summary>Wird gesetzt, um einen nicht erreichbaren SMTP-Server zu simulieren.</summary>
    public Exception? FehlerBeimVerbinden { get; set; }

    /// <summary>Liefert für eine Mail eine Exception, um einen Sendefehler zu simulieren.</summary>
    public Func<MimeMessage, Exception?>? FehlerBeimSenden { get; set; }

    public Task<IMailVerbindung> VerbindenAsync(CancellationToken abbruch)
    {
        if (FehlerBeimVerbinden is not null)
            throw FehlerBeimVerbinden;

        Verbindungen++;
        return Task.FromResult<IMailVerbindung>(new Verbindung(this));
    }

    private sealed class Verbindung(FakeMailTransport transport) : IMailVerbindung
    {
        public Task SendenAsync(MimeMessage nachricht, CancellationToken abbruch)
        {
            if (transport.FehlerBeimSenden?.Invoke(nachricht) is { } fehler)
                throw fehler;

            if (nachricht.From.Count == 0)
                nachricht.From.Add(new MailboxAddress(MailKitTransport.AbsenderName, "system@test.invalid"));

            transport._gesendet.Add(nachricht);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
