using BudoShurenWebsite.Services;
using BudoShurenWebsite.Tests.Infrastruktur;

namespace BudoShurenWebsite.Tests.Unit.Mail;

/// <summary>Der bestehende Direktversand muss sich nach der Umstellung auf IMailTransport genauso verhalten.</summary>
[Trait("Category", "Unit")]
public class EmailSenderTests
{
    private readonly FakeMailTransport _transport = new();

    [Fact]
    public async Task SendEmailAsync_versendet_eine_Html_Mail_ueber_eine_eigene_Verbindung()
    {
        var sender = new EmailSender(_transport);

        await sender.SendEmailAsync("mitglied@example.org", "Betreff", "<p>Hallo</p>");

        _transport.Verbindungen.ShouldBe(1);
        var mail = _transport.Gesendet.ShouldHaveSingleItem();
        mail.To.Mailboxes.Single().Address.ShouldBe("mitglied@example.org");
        mail.Subject.ShouldBe("Betreff");
        mail.HtmlBody.ShouldBe("<p>Hallo</p>");
    }

    [Fact]
    public async Task SendPlainTextEmailAsync_versendet_reinen_Text()
    {
        var sender = new EmailSender(_transport);

        await sender.SendPlainTextEmailAsync("mitglied@example.org", "Betreff", "Hallo");

        var mail = _transport.Gesendet.ShouldHaveSingleItem();
        mail.TextBody.ShouldBe("Hallo");
        mail.HtmlBody.ShouldBeNull();
    }

    [Fact]
    public async Task Fehlende_Adresse_wird_wie_bisher_abgelehnt()
    {
        var sender = new EmailSender(_transport);

        await Should.ThrowAsync<NullReferenceException>(() => sender.SendEmailAsync("", "Betreff", "x"));

        _transport.Verbindungen.ShouldBe(0);
    }
}
