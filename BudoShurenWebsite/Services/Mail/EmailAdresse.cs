using MimeKit;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>Prüfen und Vereinheitlichen von E-Mail-Adressen.</summary>
    public static class EmailAdresse
    {
        /// <summary>Nur eine einzelne, reine Adresse ("name@domain"), kein "Name &lt;adresse&gt;" und keine Liste.</summary>
        public static bool IstGueltig(string? adresse) =>
            !string.IsNullOrWhiteSpace(adresse)
            && MailboxAddress.TryParse(adresse.Trim(), out var postfach)
            && string.IsNullOrEmpty(postfach.Name)
            && postfach.Address.Contains('@');

        /// <summary>Für Vergleiche und eindeutige Indizes: ohne Leerzeichen, kleingeschrieben.</summary>
        public static string Normalisieren(string adresse) => adresse.Trim().ToLowerInvariant();
    }
}
