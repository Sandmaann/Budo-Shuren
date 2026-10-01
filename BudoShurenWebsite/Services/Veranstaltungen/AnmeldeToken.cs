using System.Buffers.Text;
using System.Security.Cryptography;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <param name="Klartext">Steht nur im Link der Mail, wird nie gespeichert.</param>
    /// <param name="Hash">SHA-256 des Tokens, wird in der Datenbank gespeichert.</param>
    public sealed record AnmeldeTokenPaar(string Klartext, byte[] Hash);

    /// <summary>
    /// Zufällige Tokens für Bestätigungs-, Verwaltungs- und Abmeldelinks (32 Byte, Base64Url, 43 Zeichen).
    /// In der Datenbank liegt nur der Hash: wer die Datenbank liest, kann damit keine Links bauen.
    /// </summary>
    public static class AnmeldeToken
    {
        private const int Laenge = 32;

        public static AnmeldeTokenPaar Erzeugen()
        {
            var bytes = RandomNumberGenerator.GetBytes(Laenge);
            return new AnmeldeTokenPaar(Base64Url.EncodeToString(bytes), SHA256.HashData(bytes));
        }

        /// <summary>Hash eines Tokens aus einem Link; null, wenn der Text kein gültiges Token ist.</summary>
        public static byte[]? Hash(string? klartext)
        {
            if (string.IsNullOrEmpty(klartext) || klartext.Length != Base64Url.GetEncodedLength(Laenge))
                return null;

            // TryDecodeFromChars wirft bei ungültigen Zeichen eine FormatException statt false zu liefern;
            // ein manipulierter Link soll aber nur "ungültig" sein, kein Serverfehler
            if (!klartext.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                return null;

            var bytes = new byte[Laenge];
            return Base64Url.TryDecodeFromChars(klartext, bytes, out var geschrieben) && geschrieben == Laenge
                ? SHA256.HashData(bytes)
                : null;
        }
    }
}
