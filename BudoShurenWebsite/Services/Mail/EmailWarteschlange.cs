using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>
    /// Reiht Mails zum Versand ein (Outbox-Muster): Die Mail wird im selben DbContext wie die fachliche
    /// Änderung hinzugefügt und nur versendet, wenn der Aufrufer erfolgreich speichert.
    /// </summary>
    public interface IEmailWarteschlange
    {
        /// <summary>Fügt die Mail dem Kontext hinzu. Gespeichert wird mit dem SaveChanges des Aufrufers.</summary>
        void Hinzufuegen(ApplicationDbContext kontext, AusgehendeEmail email);

        /// <summary>Nach dem Speichern aufrufen, damit der Versand sofort startet statt bei der nächsten Abfrage.</summary>
        void VersandAnstossen();
    }

    public sealed class EmailWarteschlange : IEmailWarteschlange
    {
        private readonly TimeProvider _zeit;
        private readonly EmailVersandSignal _signal;

        public EmailWarteschlange(TimeProvider zeit, EmailVersandSignal signal)
        {
            _zeit = zeit;
            _signal = signal;
        }

        public void Hinzufuegen(ApplicationDbContext kontext, AusgehendeEmail email)
        {
            // Ungültige Adressen sofort beim Aufrufer melden statt erst beim Versand
            PruefeAdresse(email.An, nameof(email.An));
            if (email.AntwortAn is not null)
                PruefeAdresse(email.AntwortAn, nameof(email.AntwortAn));
            if (string.IsNullOrWhiteSpace(email.Betreff))
                throw new ArgumentException("Der Betreff fehlt.", nameof(email));

            var jetzt = _zeit.GetUtcNow().UtcDateTime;
            kontext.EmailAusgang.Add(new EmailAusgang
            {
                An = email.An.Trim(),
                AntwortAn = email.AntwortAn?.Trim(),
                Betreff = email.Betreff,
                Html = email.Html,
                AnhaengeJson = EmailNachrichtFabrik.AnhaengeSchreiben(email.Anhaenge),
                Prioritaet = email.Prioritaet,
                Status = EmailStatus.Wartend,
                ErstelltUtc = jetzt,
                FaelligAbUtc = jetzt,
                BezugTyp = email.BezugTyp,
                BezugId = email.BezugId
            });
        }

        public void VersandAnstossen() => _signal.Ausloesen();

        private static void PruefeAdresse(string adresse, string feld)
        {
            if (!EmailAdresse.IstGueltig(adresse))
                throw new ArgumentException($"Ungültige E-Mail-Adresse: \"{adresse}\".", feld);
        }
    }
}
