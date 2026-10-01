using BudoShurenWebsite.Models;
using MimeKit;
using System.Text.Json;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>Wandelt Einträge der Warteschlange in MimeMessages um und (de)serialisiert Anhänge.</summary>
    public static class EmailNachrichtFabrik
    {
        public static MimeMessage Erstellen(EmailAusgang mail)
        {
            var nachricht = new MimeMessage();
            nachricht.To.Add(MailboxAddress.Parse(mail.An));
            if (!string.IsNullOrWhiteSpace(mail.AntwortAn))
                nachricht.ReplyTo.Add(MailboxAddress.Parse(mail.AntwortAn));
            nachricht.Subject = mail.Betreff;

            var inhalt = new BodyBuilder { HtmlBody = mail.Html };
            foreach (var anhang in AnhaengeLesen(mail.AnhaengeJson))
                inhalt.Attachments.Add(anhang.Dateiname, anhang.Inhalt, ContentType.Parse(anhang.MimeTyp));
            nachricht.Body = inhalt.ToMessageBody();

            return nachricht;
        }

        public static string? AnhaengeSchreiben(IReadOnlyList<EmailAnhang> anhaenge) =>
            anhaenge.Count == 0 ? null : JsonSerializer.Serialize(anhaenge);

        public static IReadOnlyList<EmailAnhang> AnhaengeLesen(string? json) =>
            string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<List<EmailAnhang>>(json) ?? [];
    }
}
