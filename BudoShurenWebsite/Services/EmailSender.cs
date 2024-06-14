using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using MimeKit;
using System.Threading.Tasks;

namespace BudoShurenWebsite.Services
{
    // Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
    internal sealed class EmailSender : IEmailSender<ApplicationUser>
    {
        //private readonly string smtpServer = "smtp.ionos.com";
        //private readonly int smtpPort = 587; // oder 465 für SSL
        //private readonly string smtpUser = "ihre-email@ionos.com";
        //private readonly string smtpPassword = "IhrPasswort";

        private readonly IEmailSender emailSender = new NoOpEmailSender();
        private readonly IDbContextFactory<ApplicationDbContext> dbFactory;

        public EmailSender(IDbContextFactory<ApplicationDbContext> factory)
        {
            this.dbFactory = factory;
        }

        private async Task<EmailSetting> GetEmailSettings()
        {
            using var context = dbFactory.CreateDbContext();
            var settings = await context.EmailSettings.FirstOrDefaultAsync(x => x.IsMain);
            if (settings == null)
            {
                settings = await context.EmailSettings.FirstOrDefaultAsync(x => x.IsMain);
            }
            if (settings == null)
                throw new NullReferenceException("No email settings found in database.");

            if (string.IsNullOrWhiteSpace(settings.SmtpServer))
                throw new NullReferenceException("No SMTP server found in email settings.");
            if (string.IsNullOrWhiteSpace(settings.SmtpUser))
                throw new NullReferenceException("No SMTP user found in email settings.");


            return settings;
        }

        public async Task SendPlainTextEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new NullReferenceException("No email address provided.");
            if (string.IsNullOrWhiteSpace(subject))
                throw new NullReferenceException("No subject provided.");
            if (string.IsNullOrWhiteSpace(htmlMessage))
                throw new NullReferenceException("No message provided.");

            var settings = await GetEmailSettings();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Budo Shuren Dojo", settings.SmtpUser));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = htmlMessage };

            using var client = new SmtpClient();
            await client.ConnectAsync(settings.SmtpServer, settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new NullReferenceException("No email address provided.");
            if (string.IsNullOrWhiteSpace(subject))
                throw new NullReferenceException("No subject provided.");
            if (string.IsNullOrWhiteSpace(htmlMessage))
                throw new NullReferenceException("No message provided.");

            var settings = await GetEmailSettings();

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Budo Shuren Dojo", settings.SmtpUser));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlMessage };

            using var client = new SmtpClient();
            await client.ConnectAsync(settings.SmtpServer, settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword);
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
