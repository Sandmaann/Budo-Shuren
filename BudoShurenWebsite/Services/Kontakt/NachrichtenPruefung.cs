using System.Text.RegularExpressions;

namespace BudoShurenWebsite.Services.Kontakt
{
    /// <summary>
    /// Prüft den Text einer Nachricht aus dem Kontaktformular auf typische Merkmale von Werbung.
    /// Bewusst wenige, klare Regeln: wer abgelehnt wird, bekommt gesagt, woran es liegt, und kann es ändern.
    /// </summary>
    public static partial class NachrichtenPruefung
    {
        /// <summary>Ein Link ist erlaubt (z. B. auf einen Verein oder ein Video), mehrere nicht.</summary>
        public const int ErlaubteLinks = 1;

        // Mit dem Rest der Adresse, damit "https://www.example.org" als ein Link zählt
        [GeneratedRegex(@"(?:https?://|\bwww\.)\S*", RegexOptions.IgnoreCase)]
        private static partial Regex Link();

        // Links in HTML oder BBCode schreibt kein Mensch in ein Kontaktformular
        [GeneratedRegex(@"<\s*a\b|\bhref\s*=|\[/?(url|link)\b", RegexOptions.IgnoreCase)]
        private static partial Regex LinkAuszeichnung();

        /// <returns>null, wenn der Text unauffällig ist, sonst der Grund (für das Protokoll).</returns>
        public static string? Beanstandung(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            if (LinkAuszeichnung().IsMatch(text))
                return "Link als HTML oder BBCode";

            var links = Link().Count(text);
            return links > ErlaubteLinks ? $"{links} Links" : null;
        }
    }
}
