using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Vorbereitete Texte (Markdown) für Rundmails, die der Organisator vor dem Senden noch anpassen kann.</summary>
    public static class NachrichtVorlagen
    {
        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>Nach einer Änderung von Terminen oder Ort (Plan 6.2): die aktuellen Termine und der Ort.</summary>
        public static (string Betreff, string Text) Terminaenderung(VeranstaltungUebersicht v)
        {
            var text = new StringBuilder();
            text.AppendLine("bei der Veranstaltung haben sich Termin oder Ort geändert. Es gilt jetzt:");
            text.AppendLine();
            foreach (var tag in v.Tage.Where(t => !t.Abgesagt).OrderBy(t => t.Datum))
            {
                text.Append($"- {tag.Datum.ToString("dddd, dd.MM.yyyy", Deutsch)}, {tag.Beginn:HH\\:mm}–{tag.Ende:HH\\:mm} Uhr");
                text.AppendLine(string.IsNullOrWhiteSpace(tag.Titel) ? "" : $" ({tag.Titel})");
            }
            if (!string.IsNullOrWhiteSpace(v.Ort))
            {
                text.AppendLine();
                text.AppendLine($"**Ort:** {v.Ort}");
            }
            text.AppendLine();
            text.Append("Falls du unter diesen Bedingungen nicht teilnehmen kannst, melde dich bitte über den Link aus deiner Bestätigungs-E-Mail ab.");

            return ($"Änderung: {v.Titel}", text.ToString());
        }

        // Typische Anreden am Textanfang, z. B. "Hallo zusammen," oder "Liebe Teilnehmer"
        private static readonly Regex Anrede = new(
            @"^\s*(hallo|hi|hey|servus|moin|liebe[rs]?|guten\s+(tag|morgen|abend)|sehr\s+geehrte[rs]?|grüß\s+gott)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Beginnt der Text selbst mit einer Anrede? Die Mail beginnt schon mit "Hallo Vorname," (VeranstaltungMailVorlagen.Nachricht),
        /// die Anrede stünde dann doppelt.
        /// </summary>
        public static bool BeginntMitAnrede(string? text) => !string.IsNullOrWhiteSpace(text) && Anrede.IsMatch(text);
    }
}
