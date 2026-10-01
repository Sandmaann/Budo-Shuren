using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using System.Security.Cryptography;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Einfacher Spam-Schutz für öffentliche Formulare, ohne Captcha:
    /// ein verstecktes Feld, das Menschen nicht ausfüllen (Honeypot), und eine Mindestzeit
    /// zwischen Laden und Absenden. Der Ladezeitpunkt steht verschlüsselt im Formular (Data Protection).
    /// </summary>
    public sealed class FormularSchutz
    {
        public static readonly TimeSpan Mindestdauer = TimeSpan.FromSeconds(3);

        private readonly IDataProtector _schutz;
        private readonly TimeProvider _zeit;

        public FormularSchutz(IDataProtectionProvider dataProtection, TimeProvider zeit)
        {
            _schutz = dataProtection.CreateProtector("Veranstaltungen.FormularSchutz.v1");
            _zeit = zeit;
        }

        /// <summary>Für ein verstecktes Feld beim Ausliefern des Formulars.</summary>
        public string Zeitstempel() =>
            _schutz.Protect(_zeit.GetUtcNow().UtcTicks.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// true, wenn das Absenden nach einem Bot aussieht: Honeypot gefüllt, Zeitstempel fehlt,
        /// ist manipuliert oder das Formular wurde schneller als in Mindestdauer abgeschickt.
        /// </summary>
        public bool IstVerdaechtig(string? honeypot, string? zeitstempel)
        {
            if (!string.IsNullOrWhiteSpace(honeypot) || string.IsNullOrEmpty(zeitstempel))
                return true;

            try
            {
                var ticks = long.Parse(_schutz.Unprotect(zeitstempel), CultureInfo.InvariantCulture);
                var geladen = new DateTimeOffset(ticks, TimeSpan.Zero);
                return _zeit.GetUtcNow() - geladen < Mindestdauer;
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException or OverflowException or ArgumentOutOfRangeException)
            {
                return true;
            }
        }
    }
}
