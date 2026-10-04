using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services.Mail;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System.Threading.Tasks;

namespace BudoShurenWebsite.Services
{
    // Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
    internal sealed class EmailSender : IEmailSender<ApplicationUser>
    {
        private readonly IEmailSender emailSender = new NoOpEmailSender();
        private readonly IMailTransport _transport;

        public EmailSender(IMailTransport transport)
        {
            _transport = transport;
        }

        public async Task SendPlainTextEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new NullReferenceException("No email address provided.");
            if (string.IsNullOrWhiteSpace(subject))
                throw new NullReferenceException("No subject provided.");
            if (string.IsNullOrWhiteSpace(htmlMessage))
                throw new NullReferenceException("No message provided.");

            await SendenAsync(email, subject, new TextPart("plain") { Text = htmlMessage });
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new NullReferenceException("No email address provided.");
            if (string.IsNullOrWhiteSpace(subject))
                throw new NullReferenceException("No subject provided.");
            if (string.IsNullOrWhiteSpace(htmlMessage))
                throw new NullReferenceException("No message provided.");

            await SendenAsync(email, subject, new TextPart("html") { Text = htmlMessage });
        }

        // Direktversand (ohne Warteschlange); Absender setzt der Transport (System-Adresse aus EmailSettings)
        private async Task SendenAsync(string email, string subject, MimeEntity body)
        {
            var message = new MimeMessage();
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;
            message.Body = body;

            await using var verbindung = await _transport.VerbindenAsync(CancellationToken.None);
            await verbindung.SendenAsync(message, CancellationToken.None);
        }

        // Wie die Buttons der Veranstaltungs-Mails (VeranstaltungMailVorlagen.Button): Grün der Website mit hellerem Rand,
        // auf hellem wie auf dunklem Hintergrund erkennbar. Direkt am Link, weil manche Mailprogramme <style> entfernen.
        private const string ButtonStil = "display:inline-block;padding:10px 20px;background-color:#2A6749;border:1px solid #6FB08F;color:#ffffff;text-decoration:none;";

        public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        {
            string subject = "Bitte bestätige deine E-Mail Adresse";
            string text = HtmlLayout_EmailConfirmation.Replace("[CONFIRMLINK]", $"<a href='{confirmationLink}' style='{ButtonStil}'>E-Mail Adresse bestätigen</a>");

            await SendEmailAsync(email, subject, text);
        }
        public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        {
            string subject = "Passwort zurücksetzen";
            string text = HtmlLayout_PasswortResetLink.Replace("[RESETLINK]", $"<a href='{resetLink}' style='{ButtonStil}'>Passwort jetzt zurücksetzen</a>");

            await SendEmailAsync(email, subject, text);
        }
        public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        {
            string subject = "Passwort zurücksetzen";
            string text = HtmlLayout_PasswortResetCode.Replace("[ResetCode]", resetCode);

            await SendEmailAsync(email, subject, text);
        }
        public async Task SendAccountVerifiedInfoAsync(ApplicationUser user, string email)
        {
            string subject = "Dein Zugang wurde bestätigt";
            string text = HtmlLayout_AccountVerified.Replace("[NAME]", user.Vorname ?? user.Name ?? user.UserName ?? "du");
            await SendEmailAsync(email, subject, text);
        }  
        
        public async Task SendAccountDeclinedInfoAsync(ApplicationUser user, string email)
        {
            string subject = "Dein Zugang wurde abgelehnt";
            string text = HtmlLayout_AccountDeclined.Replace("[NAME]", user.Vorname ?? user.Name ?? user.UserName ?? "du");
            await SendEmailAsync(email, subject, text);
        }

        public async Task SendKontaktformularConfirmation(string nachricht, string email)
        {
            string subject = "Bestätigung Kontaktformular Budo-Shuren-Dojo";
            string text = HtmlLayout_InteresseBestätigung.Replace("[NACHRICHT]", nachricht);
            await SendEmailAsync(email, subject, text);
        }

        public static readonly string HtmlLayout_InteresseBestätigung = @"<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
        p.blockquote {
          font: 14px/22px normal helvetica, sans-serif;
          border-left: 3px solid #ccc;
        } 
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Vielen Dank für dein Interesse!</p>
            <p>Wir haben deine Nachricht an den Abteilungsleiter weitergeleitet. Du solltest in Kürze eine Antwort erhalten.</p>
            <p>Diese Nachricht haben wir von dir bekommen:</p>
            <br>            
            <p class=""blockquote"">
            [NACHRICHT]
            </p>
            <br>
            <br>
            <p>Mit freundlichen Grüßen</p>
            <p>Dein Budo Shuren Dojo</p>            
        </div>
        <div class=""footer"">
<p>Folge uns auf <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a> für weitere Updates.</p>
        </div>
    </div>
</body>
</html>
";

        public static readonly string HtmlLayout_InteresseFormular = @"<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
        p.blockquote {
            font: 14px/22px normal helvetica, sans-serif;
            border-left: 3px solid #ccc;
        } 
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Hallo [NAME],</p>
            <p>du hast von unserer Webseite eine Anfrage bekommen. Wenn sich in deiner Abteilung mehrere Leute um das Kontaktformular kümmern, sprecht euch bitte ab!</p>
            <p>Zur Anfrage wurde folgende E-Mail für den Kontakt angegeben:</p>
            <p><strong><a href=""mailto:[EMAIL]"">[EMAIL]</a></strong></p>
            <p>Und hier folgt die Nachricht:</p>
            <br>            
            <p class=""blockquote"">
            [NACHRICHT]
            </p>
            <br>
        </div>
        <div class=""footer"">
            <p>Hier geht's direkt zu <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a>.</p>
        </div>
    </div>
</body>
</html>
";

        public static readonly string HtmlLayout_NewUser = @"<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Hallo [NAME],</p>
            <p>für deine Abteilung hat sich ein neuer Benutzer an der Budo-Shuren-Dojo Webseite registriert.</p>
            <p>Bitte prüfe doch, ob der zu deinen Leuten gehört und aktiviere seinen Zugang im Mitgliederbereich der Webseite.</p>         
            <br>
            <p>よろしくお願いします</p>
        </div>
        <div class=""footer"">
            <p>Hier geht's direkt zu <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a>.</p>
        </div>
    </div>
</body>
</html>
";

        private static readonly string HtmlLayout_EmailConfirmation = @"<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Liebes Mitglied,</p>
            <p>willkommen im Budo Shuren Dojo! Wir freuen uns, dass du dich entschieden hast, Teil unserer Gemeinschaft zu werden.</p>
            
            <p>Um die Erstellung deines Kontos abzuschließen und deinen Zugang zu aktivieren, klicke bitte auf den folgenden Link:</p>         
            [CONFIRMLINK]
            <br>
            <p>Bitte beachte, dass der Link aus Sicherheitsgründen nur 24 Stunden gültig ist.</p>
            <br>
            <p>Falls du diese E-Mail irrtümlich erhalten hast oder du dich nicht im Budo Shuren Dojo registriert hast, ignoriere bitte diese Nachricht.</p>
            <br>
            <p>Bei Fragen oder Problemen stehen wir dir gerne zur Verfügung. Kontaktiere einfach deinen Abteilungsleiter!</p>
            <br>            
            <p>Vielen Dank und herzlich willkommen!</p>
        </div>
        <div class=""footer"">
            <p>Folge uns auf <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a> für weitere Updates.</p>
        </div>
    </div>
</body>
</html>
";
        private static readonly string HtmlLayout_PasswortResetLink = @"<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Liebes Mitglied,</p>
            <p>wir haben eine Anfrage zum Zurücksetzen deines Passworts erhalten. Wenn du diese Anfrage nicht gestellt haben, kannstu du diese E-Mail ignorieren.</p>
            <p>Um dein Passwort zurückzusetzen, klicke bitte auf den folgenden Link:</p>         
            [RESETLINK]
            <br>
            <p>Gebe diesen Code auf der Seite zum Zurücksetzen des Passworts ein. Der Code ist 24 Stunden gültig.</p>
            <p>Wenn du weiter Probleme beim Zurücksetzen deines Passworts hast, kontakriere bitte deinen Abteilungsleiter.</p>
        </div>
        <div class=""footer"">
            <p>Folge uns auf <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a> für weitere Updates.</p>
        </div>
    </div>
</body>
</html>
";

        private static readonly string HtmlLayout_PasswortResetCode = @"<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Liebes Mitglied,</p>
            <p>wir haben eine Anfrage zum Zurücksetzen deines Passworts erhalten. Wenn du diese Anfrage nicht gestellt haben, kannstu du diese E-Mail ignorieren.</p>
            <p>Um dein Passwort zurückzusetzen, verwende bitte den folgenden Code:</p>         
            <p><strong>[ResetCode]:</strong>
            <br>
            <p>Gebe diesen Code auf der Seite zum Zurücksetzen des Passworts ein. Der Code ist 30 Minuten lang gültig.</p>
            <p>Wenn du weiter Probleme beim Zurücksetzen deines Passworts hast, kontakriere bitte deinen Abteilungsleiter.</p>
        </div>
        <div class=""footer"">
            <p>Folge uns auf <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a> für weitere Updates.</p>
        </div>
    </div>
</body>
</html>
";

        private static readonly string HtmlLayout_AccountVerified = @"<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Hallo [NAME],</p>
            <p>dein Zugang für die Budo Shuren Dojo Webseite wurde gerade bestätigt.</p>
            <p>Du kannst dich jetzt anmelden.</p>         
            <br>
            <p>Bei Fragen oder Problemen stehen wir dir gerne zur Verfügung. Kontaktiere einfach deinen Abteilungsleiter!</p>
            <br>            
            <p>Vielen Dank und herzlich willkommen!</p>
        </div>
        </div>
        <div class=""footer"">
            <p>Hier geht's direkt zu <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a>.</p>
        </div>
    </div>
</body>
</html>";   
        
        private static readonly string HtmlLayout_AccountDeclined = @"<html>
<head>
    <style>
        body, html {
            margin: 0;
            padding: 0;
            width: 100%;
            font-family: Arial, sans-serif;
        }
        .container {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #ffffff;
            color: #000000;
        }
        .header, .footer {
            text-align: center;
            padding: 10px 0;
        }
        .content {
            text-align: center;
            padding: 20px 0;
        }
        .button {
            display: inline-block;
            padding: 10px 20px;
            background-color: #2A6749;
            border: 1px solid #6FB08F;
            color: #ffffff;
            text-decoration: none;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>武道修練道場</h1>
            <h2>Budo Shuren Dojo</h2>
        </div>
        <div class=""content"">
            <p>Hallo [NAME],</p>
            <p>dein Zugang für die Budo Shuren Dojo Webseite wurde von einem Abteilungsleiter leider abgelehnt.</p>
            <p>Dein Zugang wurde dadurch gelöscht und du erhältst keinen Zugriff auf den Mitgliederbereich.</p>         
            <p>Wenn dein Zugang fälschlicherweise abgelehnt worden ist:</p>
            <p>Lege deinen Zugang noch einmal an. Deine verwendeten Zugangsdaten sind jetzt wieder verfügbar.</p>
            <p>Besprich dich bitte direkt mit deinem Abteilungsleiter. Teile ihm deine Nutzerdaten (Name/Email) direkt mit, damit dein Zugang korrekt identifiziert werden kann.</p>
            <br>
            <p>Bei Fragen oder Problemen stehen wir dir gerne zur Verfügung. Kontaktiere einfach deinen Abteilungsleiter!</p>
            <br>            
            <p>Vielen Dank für dein Verständnis.</p>
        </div>
        </div>
        <div class=""footer"">
            <p>Hier geht's direkt zu <a href=""https://www.budo-shuren-dojo.de/"" style=""color: #000000;"">unserer Webseite</a>.</p>
        </div>
    </div>
</body>
</html>";
    }
}
