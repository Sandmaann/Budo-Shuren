using BudoShurenWebsite.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using System.Threading.Tasks;

namespace BudoShurenWebsite.Components.Account
{
    // Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
    internal sealed class IdentityNoOpEmailSender : IEmailSender<ApplicationUser>
    {
        private readonly string smtpServer = "smtp.ionos.com";
        private readonly int smtpPort = 587; // oder 465 für SSL
        private readonly string smtpUser = "ihre-email@ionos.com";
        private readonly string smtpPassword = "IhrPasswort";

        private readonly IEmailSender emailSender = new NoOpEmailSender();

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Budo Shuren Dojo", smtpUser));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlMessage };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
        {
            string subject = "Bitte bestätige deine E-Mail Adresse";
            string text = $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.";

            await SendEmailAsync(email, subject, text);
        }
        public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        {
            string subject = "Passwort zurücksetzen";
            string text = $"Please reset your password by <a href='{resetLink}'>clicking here</a>.";

            await SendEmailAsync(email, subject, text);
        }
        public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
        {
            string subject = "Passwort zurücksetzen";
            string text = $"Please reset your password using the following code: {resetCode}";

            await SendEmailAsync(email, subject, text);
        }


        //public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        //    emailSender.SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

        //public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        //    emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

        //public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        //    emailSender.SendEmailAsync(email, "Reset your password", $"Please reset your password using the following code: {resetCode}");
    }
}
