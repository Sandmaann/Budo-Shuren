using System.Text;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services.Mail;
using MimeKit;

namespace BudoShurenWebsite.Tests.Unit.Mail;

[Trait("Category", "Unit")]
public class EmailNachrichtFabrikTests
{
    [Fact]
    public void Erstellt_Html_Mail_mit_Empfaenger_Betreff_und_Antwortadresse()
    {
        var mail = new EmailAusgang
        {
            An = "teilnehmer@example.org",
            AntwortAn = "kontakt@example.org",
            Betreff = "Anmeldung bestätigt",
            Html = "<p>Hallo</p>"
        };

        var nachricht = EmailNachrichtFabrik.Erstellen(mail);

        nachricht.To.Mailboxes.Single().Address.ShouldBe("teilnehmer@example.org");
        nachricht.ReplyTo.Mailboxes.Single().Address.ShouldBe("kontakt@example.org");
        nachricht.From.Count.ShouldBe(0, "der Absender wird vom Transport gesetzt");
        nachricht.Subject.ShouldBe("Anmeldung bestätigt");
        nachricht.HtmlBody.ShouldBe("<p>Hallo</p>");
        nachricht.Attachments.ShouldBeEmpty();
    }

    [Fact]
    public void Ohne_Antwortadresse_bleibt_ReplyTo_leer()
    {
        var nachricht = EmailNachrichtFabrik.Erstellen(new EmailAusgang { An = "a@example.org", Betreff = "x", Html = "y" });

        nachricht.ReplyTo.Count.ShouldBe(0);
    }

    [Fact]
    public void Anhaenge_ueberstehen_Speichern_und_Laden()
    {
        var ics = Encoding.UTF8.GetBytes("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");
        var json = EmailNachrichtFabrik.AnhaengeSchreiben([new EmailAnhang("termin.ics", "text/calendar", ics)]);

        var nachricht = EmailNachrichtFabrik.Erstellen(new EmailAusgang
        {
            An = "a@example.org", Betreff = "x", Html = "y", AnhaengeJson = json
        });

        var anhang = nachricht.Attachments.OfType<MimePart>().Single();
        anhang.FileName.ShouldBe("termin.ics");
        anhang.ContentType.MimeType.ShouldBe("text/calendar");
        using var inhalt = new MemoryStream();
        anhang.Content.ShouldNotBeNull().DecodeTo(inhalt, TestContext.Current.CancellationToken);
        inhalt.ToArray().ShouldBe(ics);
    }

    [Fact]
    public void Keine_Anhaenge_werden_als_null_gespeichert()
    {
        EmailNachrichtFabrik.AnhaengeSchreiben([]).ShouldBeNull();
        EmailNachrichtFabrik.AnhaengeLesen(null).ShouldBeEmpty();
    }
}
