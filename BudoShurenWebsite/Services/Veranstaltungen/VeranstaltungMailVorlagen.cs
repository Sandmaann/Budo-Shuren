using BudoShurenWebsite.Models.Veranstaltungen;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    public sealed record MailInhalt(string Betreff, string Html);

    /// <summary>
    /// Mails an Teilnehmer und Info-Adressen. Ein gemeinsames Layout wie die übrigen Mails der Website;
    /// alle Werte aus Eingaben werden HTML-kodiert (die älteren Vorlagen in EmailSender tun das nicht).
    /// </summary>
    public static class VeranstaltungMailVorlagen
    {
        // Maskiert < > & " ', lässt aber Umlaute lesbar (HtmlEncoder.Default würde sie als &#xE4; schreiben)
        private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

        public static MailInhalt OptIn(Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, Anmeldung a, string bestaetigenUrl, int reservierungStunden) => new(
            $"Bitte bestätige deine Anmeldung: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>danke für deine Anmeldung zu <strong>{E(v.Titel)}</strong>. Bitte bestätige sie mit einem Klick, damit sie gültig wird:</p>" +
                Button(bestaetigenUrl, "Anmeldung bestätigen") +
                $"<p>Wir halten deinen Platz {reservierungStunden} Stunden frei. Danach verfällt die unbestätigte Anmeldung.</p>" +
                Zusammenfassung(v, tage, a) +
                "<p style=\"font-size:12px;color:#555;\">Du hast dich nicht angemeldet? Dann ignoriere diese E-Mail einfach.</p>"));

        public static MailInhalt Bestaetigung(Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, Anmeldung a, string verwaltungUrl) => new(
            $"Anmeldung bestätigt: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>du bist für <strong>{E(v.Titel)}</strong> angemeldet. Wir freuen uns auf dich!</p>" +
                Zusammenfassung(v, tage, a) +
                "<p>Über diesen Link kannst du deine Anmeldung jederzeit ansehen, ändern oder dich abmelden:</p>" +
                Button(verwaltungUrl, "Meine Anmeldung") +
                "<p style=\"font-size:12px;color:#555;\">Bitte gib diesen Link nicht weiter: Wer ihn hat, kann deine Anmeldung ändern.</p>"));

        /// <summary>Für eine bereits bestehende Anmeldung (erneutes Absenden des Formulars).</summary>
        public static MailInhalt LinkErneut(Veranstaltung v, Anmeldung a, string url, bool nochUnbestaetigt) => new(
            nochUnbestaetigt ? $"Bitte bestätige deine Anmeldung: {v.Titel}" : $"Deine Anmeldung: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                (nochUnbestaetigt
                    ? $"<p>du hast dich bereits für <strong>{E(v.Titel)}</strong> angemeldet, die Anmeldung aber noch nicht bestätigt. Hier ist ein neuer Bestätigungslink:</p>" +
                      Button(url, "Anmeldung bestätigen")
                    : $"<p>du bist bereits für <strong>{E(v.Titel)}</strong> angemeldet. Über diesen Link kannst du deine Anmeldung ansehen und ändern:</p>" +
                      Button(url, "Meine Anmeldung")) +
                "<p style=\"font-size:12px;color:#555;\">Ältere Links aus früheren E-Mails gelten nicht mehr.</p>"));

        /// <summary>Für eine abgelehnte Adresse: neutral, ohne Gründe, mit Verweis auf den Kontakt.</summary>
        public static MailInhalt AnmeldungNichtMoeglich(Veranstaltung v) => new(
            $"Deine Anmeldung: {v.Titel}",
            Layout(
                $"<p>Hallo,</p><p>eine Anmeldung zu <strong>{E(v.Titel)}</strong> ist mit dieser E-Mail-Adresse leider nicht möglich.</p>" +
                Kontakt(v)));

        /// <summary>An eine Info-Adresse. Enthält bewusst keinen frei wählbaren Text des Anmelders.</summary>
        public static MailInhalt InfoAnBegleitung(Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, Anmeldung a, string veranstaltungUrl, string abmeldenUrl) => new(
            $"Info: {v.Titel}",
            Layout(
                "<p>Hallo,</p>" +
                $"<p>{E(a.Vorname)} {E(a.Nachname)} hat sich für <strong>{E(v.Titel)}</strong> angemeldet und deine Adresse für Informationen zur Veranstaltung angegeben.</p>" +
                Termine(v, tage) +
                Button(veranstaltungUrl, "Zur Veranstaltung") +
                $"<p style=\"font-size:12px;color:#555;\">Du möchtest keine Infos zu dieser Veranstaltung bekommen? <a href=\"{E(abmeldenUrl)}\" style=\"color:#555;\">Hier abmelden</a>.</p>"));

        public static MailInhalt AenderungGespeichert(Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, Anmeldung a) => new(
            $"Anmeldung geändert: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>deine Anmeldung zu <strong>{E(v.Titel)}</strong> wurde geändert. So sieht sie jetzt aus:</p>" +
                Zusammenfassung(v, tage, a) +
                "<p>Weitere Änderungen sind über den Link aus deiner Bestätigungs-E-Mail möglich.</p>" +
                "<p style=\"font-size:12px;color:#555;\">Du hast nichts geändert? Dann melde dich bitte bei uns.</p>" +
                Kontakt(v)));

        public static MailInhalt AbmeldungBestaetigt(Veranstaltung v, Anmeldung a, string veranstaltungUrl) => new(
            $"Abmeldung bestätigt: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>du hast dich von <strong>{E(v.Titel)}</strong> abgemeldet. Schade, dass du nicht dabei bist!</p>" +
                "<p>Falls du doch kommen möchtest, kannst du dich über die Veranstaltungsseite erneut anmelden, solange Plätze frei sind.</p>" +
                Button(veranstaltungUrl, "Zur Veranstaltung") +
                Kontakt(v)));

        /// <summary>An die neue Adresse; erst nach dem Klick wird sie übernommen.</summary>
        public static MailInhalt EmailWechselBestaetigen(Veranstaltung v, Anmeldung a, string bestaetigenUrl) => new(
            $"Bitte bestätige deine neue E-Mail-Adresse: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>für deine Anmeldung zu <strong>{E(v.Titel)}</strong> wurde diese E-Mail-Adresse angegeben. Bitte bestätige sie:</p>" +
                Button(bestaetigenUrl, "Neue Adresse bestätigen") +
                "<p>Bis dahin gehen alle Infos weiter an die bisherige Adresse.</p>" +
                "<p style=\"font-size:12px;color:#555;\">Du kennst diese Anmeldung nicht? Dann ignoriere diese E-Mail einfach.</p>"));

        /// <summary>An die bisherige Adresse, damit ein unbemerkter Wechsel auffällt.</summary>
        public static MailInhalt EmailWechselHinweis(Veranstaltung v, Anmeldung a, string neueEmail) => new(
            $"Neue E-Mail-Adresse angegeben: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>für deine Anmeldung zu <strong>{E(v.Titel)}</strong> wurde die neue Adresse <strong>{E(neueEmail)}</strong> angegeben. " +
                "Sobald sie bestätigt ist, gehen alle Infos dorthin, und der bisherige Link zu deiner Anmeldung gilt nicht mehr.</p>" +
                "<p>Warst du das nicht? Dann melde dich bitte umgehend bei uns.</p>" +
                Kontakt(v)));

        /// <summary>An die neue Adresse nach der Bestätigung, mit neuem Verwaltungslink.</summary>
        public static MailInhalt EmailWechselAbgeschlossen(Veranstaltung v, Anmeldung a, string verwaltungUrl) => new(
            $"Neue E-Mail-Adresse bestätigt: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>deine neue E-Mail-Adresse für <strong>{E(v.Titel)}</strong> ist bestätigt. Über diesen Link kannst du deine Anmeldung ansehen und ändern:</p>" +
                Button(verwaltungUrl, "Meine Anmeldung") +
                "<p style=\"font-size:12px;color:#555;\">Ältere Links gelten nicht mehr. Bitte gib diesen Link nicht weiter.</p>"));

        /// <summary>
        /// Nachricht der Organisatoren (Rundmail, Absage). inhaltHtml ist bereits sicher gerendert (MarkdownText.SicherZuHtml
        /// bzw. eigene Bausteine); fussHtml unterscheidet Teilnehmer und Info-Adressen.
        /// </summary>
        /// <param name="vorname">Für die Anrede; null bei Info-Adressen ("Hallo,").</param>
        public static MailInhalt Nachricht(Veranstaltung v, string betreff, string inhaltHtml, string? vorname, string fussHtml) => new(
            betreff,
            Layout(
                (vorname is null ? "<p>Hallo,</p>" : $"<p>Hallo {E(vorname)},</p>") +
                $"<p>eine Nachricht zu <strong>{E(v.Titel)}</strong>:</p>" +
                $"<div style=\"margin:16px 0;\">{inhaltHtml}</div>" +
                Kontakt(v) +
                fussHtml));

        public static string FussTeilnehmer(string linkAnfordernUrl) =>
            "<p style=\"font-size:12px;color:#555;\">Deine Anmeldung kannst du über den Link aus deiner Bestätigungs-E-Mail ansehen und ändern. " +
            $"Link verloren? <a href=\"{E(linkAnfordernUrl)}\" style=\"color:#555;\">Neuen Link anfordern</a>.</p>";

        public static string FussInfo(string abmeldenUrl) =>
            "<p style=\"font-size:12px;color:#555;\">Du bekommst diese Nachricht, weil dich jemand bei der Anmeldung angegeben hat. " +
            $"<a href=\"{E(abmeldenUrl)}\" style=\"color:#555;\">Keine Infos mehr zu dieser Veranstaltung</a>.</p>";

        /// <param name="zusatzHtml">Optionaler Text der Organisatoren, bereits sicher gerendert.</param>
        public static string TagAbgesagtInhalt(VeranstaltungsTag tag, string? zusatzHtml) =>
            $"<p><strong>Der Termin am {E(TerminText.Zeile(tag))} wurde abgesagt.</strong> Die übrigen Termine finden wie geplant statt.</p>" +
            (zusatzHtml ?? "");

        public static string AbgesagtInhalt(string? zusatzHtml) =>
            "<p><strong>Die Veranstaltung wurde abgesagt.</strong> Deine Anmeldung ist damit hinfällig, du musst nichts weiter tun.</p>" +
            (zusatzHtml ?? "");

        /// <summary>Organisator lehnt eine Anmeldung ab; der Grund ist optional.</summary>
        public static MailInhalt AbgelehntDurchOrganisator(Veranstaltung v, Anmeldung a, string? grund) => new(
            $"Deine Anmeldung: {v.Titel}",
            Layout(
                $"<p>Hallo {E(a.Vorname)},</p>" +
                $"<p>leider können wir deine Anmeldung zu <strong>{E(v.Titel)}</strong> nicht annehmen.</p>" +
                (string.IsNullOrWhiteSpace(grund) ? "" : $"<p style=\"border-left:3px solid #ccc;padding-left:12px;\">{E(grund)}</p>") +
                Kontakt(v)));

        /// <summary>Ein Eintrag für die Mail "Link anfordern".</summary>
        public sealed record AngeforderterLink(string Titel, string Termin, string Url, bool NochUnbestaetigt);

        /// <summary>Alle aktiven Anmeldungen einer Adresse in einer Mail.</summary>
        public static MailInhalt LinksAngefordert(IReadOnlyList<AngeforderterLink> links)
        {
            var liste = new StringBuilder();
            foreach (var link in links)
            {
                liste.Append($"<p><strong>{E(link.Titel)}</strong><br>{E(link.Termin)}</p>");
                liste.Append(Button(link.Url, link.NochUnbestaetigt ? "Anmeldung bestätigen" : "Meine Anmeldung"));
            }

            return new MailInhalt(
                "Deine Links zu deinen Anmeldungen",
                Layout(
                    "<p>Hallo,</p><p>hier sind die Links zu deinen Anmeldungen:</p>" +
                    liste +
                    "<p style=\"font-size:12px;color:#555;\">Ältere Links aus früheren E-Mails gelten nicht mehr. Du hast keine Links angefordert? Dann ignoriere diese E-Mail einfach.</p>"));
        }

        /// <summary>Zeitraum über die nicht abgesagten Termine, z. B. "14.–15. November 2026".</summary>
        public static string Zeitraum(IReadOnlyCollection<VeranstaltungsTag> tage) =>
            TerminText.Zeitraum(tage.Where(t => !t.Abgesagt).Select(t => t.Datum));

        // ------------------------------------------------------------------------------------

        internal static string E(string? wert) => Encoder.Encode(wert ?? string.Empty);

        // Grün der Website (Tailwind "primary") mit hellerem Rand: ein schwarzer Button verschwindet, wenn das
        // Mailprogramm im Dunkelmodus den Hintergrund dunkel färbt. Grün mit weißer Schrift ist auf Weiß wie auf Dunkel erkennbar.
        internal static string Button(string url, string text) =>
            $"<p style=\"margin:24px 0;\"><a href=\"{E(url)}\" style=\"display:inline-block;padding:10px 20px;background-color:#2A6749;border:1px solid #6FB08F;color:#ffffff;text-decoration:none;\">{E(text)}</a></p>" +
            $"<p style=\"font-size:12px;color:#555;word-break:break-all;\">Falls der Button nicht funktioniert: {E(url)}</p>";

        /// <summary>Termine nach Datum gruppiert (Datum fett, darunter Uhrzeit und Titel), danach der Ort.</summary>
        private static string Termine(Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage)
        {
            var sb = new StringBuilder();
            foreach (var datum in TerminText.NachDatum(tage.Where(t => !t.Abgesagt)))
            {
                sb.Append($"<p style=\"margin:0 0 8px;\"><strong>{E(TerminText.Datum(datum.Key))}</strong>");
                foreach (var tag in datum)
                    sb.Append($"<br>{E(TerminText.Uhrzeit(tag))}").Append(string.IsNullOrWhiteSpace(tag.Titel) ? "" : $" · {E(tag.Titel)}");
                sb.Append("</p>");
            }
            if (!string.IsNullOrWhiteSpace(v.Ort))
                sb.Append($"<p style=\"margin:0 0 8px;\"><strong>Ort:</strong> {E(v.Ort)}").Append(string.IsNullOrWhiteSpace(v.Adresse) ? "" : $", {E(v.Adresse)}").Append("</p>");
            return sb.ToString();
        }

        private static string Zusammenfassung(Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, Anmeldung a)
        {
            // Bei Teilanmeldung nur die gebuchten Tage
            var gebuchteIds = a.Tage.Select(t => t.VeranstaltungsTagId).ToHashSet();
            var gebucht = gebuchteIds.Count == 0 ? tage : tage.Where(t => gebuchteIds.Contains(t.Id)).ToList();

            var personen = a.AnzahlBegleitpersonen == 0
                ? "1 Person"
                : $"{1 + a.AnzahlBegleitpersonen} Personen (du und {a.AnzahlBegleitpersonen} Begleitperson{(a.AnzahlBegleitpersonen == 1 ? "" : "en")})";

            return "<div style=\"border-left:3px solid #ccc;padding-left:12px;margin:16px 0;\">" +
                   Termine(v, gebucht) +
                   $"<p><strong>Name:</strong> {E(a.Vorname)} {E(a.Nachname)}<br><strong>Anmeldung für:</strong> {E(personen)}</p>" +
                   "</div>";
        }

        private static string Kontakt(Veranstaltung v) =>
            string.IsNullOrWhiteSpace(v.KontaktEmail)
                ? string.Empty
                : $"<p>Bei Fragen erreichst du {E(v.KontaktName ?? "die Organisatoren")} unter <a href=\"mailto:{E(v.KontaktEmail)}\" style=\"color:#000000;\">{E(v.KontaktEmail)}</a>.</p>";

        internal static string Layout(string inhalt) =>
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head>" +
            "<body style=\"margin:0;padding:0;font-family:Arial,sans-serif;color:#000000;background-color:#ffffff;\">" +
            "<div style=\"max-width:600px;margin:0 auto;padding:20px;\">" +
            "<div style=\"text-align:center;padding:10px 0;\"><h1 style=\"margin:0;\">武道修練道場</h1><h2 style=\"margin:4px 0 0;\">Budo Shuren Dojo</h2></div>" +
            $"<div style=\"padding:20px 0;line-height:1.5;\">{inhalt}<p>Mit freundlichen Grüßen<br>Dein Budo Shuren Dojo</p></div>" +
            "<div style=\"text-align:center;padding:10px 0;font-size:12px;\"><a href=\"https://www.budo-shuren-dojo.de/\" style=\"color:#000000;\">www.budo-shuren-dojo.de</a></div>" +
            "</div></body></html>";
    }
}
