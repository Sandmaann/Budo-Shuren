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

        /// <param name="tage">Alle Tage der Veranstaltung (für die Spalte "Tage" bei Teilanmeldung).</param>
        public static byte[] Erstellen(IEnumerable<Anmeldung> anmeldungen, IReadOnlyCollection<VeranstaltungsTag> tage, Teilnahmemodus modus)
        {
            var sb = new StringBuilder();
            Zeile(sb, "Nachname", "Vorname", "E-Mail", "Status", "Personen", "Begleitpersonen", "Tage", "Telefon",
                "Verein", "Graduierung", "Bemerkung", "Angemeldet am", "Quelle", "Interne Notiz");

            var tagTexte = tage.ToDictionary(t => t.Id, t => t.Datum.ToString("dd.MM.yyyy"));
            var alleTage = string.Join(", ", tage.Where(t => !t.Abgesagt).OrderBy(t => t.Datum).Select(t => tagTexte[t.Id]));

            foreach (var a in anmeldungen.OrderBy(a => a.Nachname).ThenBy(a => a.Vorname))
            {
                var gebucht = modus == Teilnahmemodus.NurGesamt
                    ? alleTage
                    : string.Join(", ", a.Tage.Select(t => t.VeranstaltungsTagId).Where(tagTexte.ContainsKey).Select(id => tagTexte[id]).Order());

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
