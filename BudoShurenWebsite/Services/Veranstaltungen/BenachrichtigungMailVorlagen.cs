using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using System.Text;
using static BudoShurenWebsite.Services.Veranstaltungen.VeranstaltungMailVorlagen;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Was in einer Benachrichtigung zu einer Anmeldung steht.</summary>
    /// <param name="Tage">Gebuchte Termine als Kurztext (TerminText.Kurz); leer bei Teilnahmemodus NurGesamt.</param>
    public sealed record BenachrichtigungsAbschnitt(string Name, int Personen, IReadOnlyList<string> Tage, IReadOnlyList<BenachrichtigungsZeile> Zeilen);

    public sealed record BenachrichtigungsZeile(DateTime ZeitpunktUtc, string Text);

    /// <param name="Max">null = unbegrenzt.</param>
    public sealed record TagStand(VeranstaltungsTag Tag, int Belegt, int? Max);

    /// <summary>
    /// Mails an Empfänger von Organisator-Benachrichtigungen (Plan 2.9). Diese gehen auch an externe Adressen,
    /// deshalb nur Name, Personen, Tage und Art der Änderung (keine Telefonnummer, Bemerkung oder Notiz).
    /// </summary>
    public static class BenachrichtigungMailVorlagen
    {
        public static MailInhalt Benachrichtigung(
            Veranstaltung v,
            bool zusammenfassung,
            IReadOnlyList<BenachrichtigungsAbschnitt> abschnitte,
            bool ausgebucht,
            bool anmeldeschluss,
            IReadOnlyList<TagStand> stand,
            string? uebersichtUrl,
            string? abmeldenUrl)
        {
            var inhalt = new StringBuilder();
            inhalt.Append($"<p>Hallo,</p><p>{(zusammenfassung ? "die Zusammenfassung" : "Neuigkeiten")} zu <strong>{E(v.Titel)}</strong>:</p>");

            if (ausgebucht)
                inhalt.Append("<p><strong>Die Veranstaltung ist jetzt ausgebucht.</strong></p>");
            if (anmeldeschluss)
                inhalt.Append("<p><strong>Der Anmeldeschluss ist erreicht.</strong></p>");

            foreach (var a in abschnitte)
            {
                inhalt.Append("<div style=\"border-left:3px solid #ccc;padding-left:12px;margin:16px 0;\">");
                inhalt.Append($"<p style=\"margin:0;\"><strong>{E(a.Name)}</strong> ({(a.Personen == 1 ? "1 Person" : $"{a.Personen} Personen")}");
                if (a.Tage.Count > 0)
                    inhalt.Append(", ").Append(E(string.Join(", ", a.Tage)));
                inhalt.Append(")</p><ul style=\"margin:4px 0;padding-left:20px;\">");
                foreach (var z in a.Zeilen)
                    inhalt.Append($"<li>{E(z.Text)} <span style=\"color:#555;font-size:12px;\">({Ortszeit.AusUtc(z.ZeitpunktUtc):dd.MM. HH:mm})</span></li>");
                inhalt.Append("</ul></div>");
            }

            if (stand.Count > 0)
            {
                inhalt.Append("<p><strong>Aktueller Stand:</strong><br>");
                var alle = stand.Select(s => s.Tag).ToList();
                foreach (var t in stand)
                    inhalt.Append(E(TerminText.Kurz(t.Tag, alle, mitTitel: true))).Append(": ").Append(t.Max is { } max ? $"{t.Belegt} von {max} Plätzen belegt" : $"{t.Belegt} Personen").Append("<br>");
                inhalt.Append("</p>");
            }

            // Nur Website-Benutzer können die Übersicht öffnen
            if (uebersichtUrl is not null)
                inhalt.Append(Button(uebersichtUrl, "Zur Übersicht"));
            inhalt.Append(Fuss(abmeldenUrl));

            var betreff = $"{(zusammenfassung ? "Zusammenfassung" : "Benachrichtigung")}: {v.Titel}{(ausgebucht ? " (ausgebucht)" : "")}";
            return new MailInhalt(betreff, Layout(inhalt.ToString()));
        }

        /// <summary>An eine freie Adresse, sobald sie eingetragen wurde: wer, wofür, wie abmelden.</summary>
        public static MailInhalt Eingetragen(Veranstaltung v, string eingetragenVon, BenachrichtigungEreignisse ereignisse, BenachrichtigungModus modus, string abmeldenUrl)
        {
            var arten = Enum.GetValues<BenachrichtigungEreignisse>()
                .Where(e => e is not BenachrichtigungEreignisse.Keine and not BenachrichtigungEreignisse.Alle && ereignisse.HasFlag(e))
                .Select(e => e.Beschreibung())
                .ToList();
            var wann = modus == BenachrichtigungModus.TaeglicheZusammenfassung
                ? $"einmal täglich gegen {v.ZusammenfassungUhrzeit:HH\\:mm} Uhr als Zusammenfassung"
                : "kurz nach der jeweiligen Änderung";

            return new MailInhalt(
                $"Benachrichtigungen zu {v.Titel}",
                Layout(
                    "<p>Hallo,</p>" +
                    $"<p>{E(eingetragenVon)} hat diese Adresse für Benachrichtigungen zu <strong>{E(v.Titel)}</strong> eingetragen.</p>" +
                    (arten.Count == 0 ? "" : $"<p>Du bekommst {wann} eine E-Mail bei: {E(string.Join(", ", arten))}.</p>") +
                    Fuss(abmeldenUrl)));
        }

        private static string Fuss(string? abmeldenUrl) =>
            abmeldenUrl is null
                ? "<p style=\"font-size:12px;color:#555;\">Einstellungen zu diesen Benachrichtigungen findest du auf der Bearbeiten-Seite der Veranstaltung.</p>"
                : $"<p style=\"font-size:12px;color:#555;\">Du möchtest diese Benachrichtigungen nicht mehr bekommen? <a href=\"{E(abmeldenUrl)}\" style=\"color:#555;\">Hier abmelden</a>. Es gilt jeweils der Link aus der neuesten E-Mail.</p>";
    }
}
