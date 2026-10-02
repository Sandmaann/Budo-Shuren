using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using System.Text;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Teilnehmerliste als CSV für Excel: UTF-8 mit BOM (Umlaute), Semikolon als Trenner (deutsches Excel),
    /// Werte in Anführungszeichen, und Schutz vor Formel-Injection (Werte wie "=..." werden nicht als Formel ausgeführt).
    /// </summary>
    public static class TeilnehmerCsv
    {
        private static readonly char[] Formelzeichen = ['=', '+', '-', '@', '\t', '\r'];

        /// <param name="tage">Alle Termine der Veranstaltung (für die Spalte "Termine").</param>
        public static byte[] Erstellen(IEnumerable<Anmeldung> anmeldungen, IReadOnlyCollection<VeranstaltungsTag> tage, Teilnahmemodus modus)
        {
            var sb = new StringBuilder();
            Zeile(sb, "Nachname", "Vorname", "E-Mail", "Status", "Personen", "Begleitpersonen", "Termine", "Telefon",
                "Verein", "Graduierung", "Bemerkung", "Angemeldet am", "Quelle", "Interne Notiz");

            var sortiert = TerminText.Sortiert(tage).ToList();
            var tagTexte = sortiert.ToDictionary(t => t.Id, t => TerminText.Kurz(t, tage, mitTitel: true));
            var alleTage = string.Join(", ", sortiert.Where(t => !t.Abgesagt).Select(t => tagTexte[t.Id]));

            foreach (var a in anmeldungen.OrderBy(a => a.Nachname).ThenBy(a => a.Vorname))
            {
                var gebucht = modus == Teilnahmemodus.NurGesamt
                    ? alleTage
                    : string.Join(", ", sortiert.Where(t => a.Tage.Any(at => at.VeranstaltungsTagId == t.Id)).Select(t => tagTexte[t.Id]));

                Zeile(sb,
                    a.Nachname,
                    a.Vorname,
                    a.Email,
                    a.Status.Beschreibung(),
                    (1 + a.AnzahlBegleitpersonen).ToString(),
                    a.AnzahlBegleitpersonen.ToString(),
                    gebucht,
                    a.Telefon,
                    a.Verein,
                    a.Graduierung,
                    a.Bemerkung,
                    Ortszeit.AusUtc(a.ErstelltUtc).ToString("dd.MM.yyyy HH:mm"),
                    a.Quelle.Beschreibung(),
                    a.AdminNotiz);
            }

            return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
        }

        private static void Zeile(StringBuilder sb, params string?[] werte) =>
            sb.Append(string.Join(";", werte.Select(Feld))).Append("\r\n");

        private static string Feld(string? wert)
        {
            var text = wert ?? string.Empty;
            // Excel würde "=...", "+...", "-...", "@..." als Formel auswerten; ein vorangestelltes ' verhindert das
            if (text.Length > 0 && Formelzeichen.Contains(text[0]))
                text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
    }
}
