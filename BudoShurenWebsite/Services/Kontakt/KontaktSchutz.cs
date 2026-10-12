using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Services.Kontakt
{
    /// <summary>
    /// Spam-Schutz des Kontaktformulars, ohne Captcha. Nacheinander: verstecktes Feld (Honeypot), Zeit zwischen
    /// Ausliefern und Absenden (<see cref="FormularSchutz"/>), Inhalt (<see cref="NachrichtenPruefung"/>) und
    /// Anzahl der Nachrichten (<see cref="KontaktLimit"/>). Der Grund der Ablehnung kommt ins Protokoll.
    /// </summary>
    public sealed class KontaktSchutz
    {
        /// <summary>
        /// So lange gilt ein ausgeliefertes Formular. Ohne Grenze ließe sich ein einmal geholter Zeitstempel
        /// beliebig oft wiederverwenden.
        /// </summary>
        public static readonly TimeSpan FormularGueltigFuer = TimeSpan.FromHours(24);

        private const string DirektSchreiben = "Du kannst auch direkt an info@budo-shuren-dojo.de schreiben.";

        private readonly FormularSchutz _formularSchutz;
        private readonly KontaktLimit _limit;

        public KontaktSchutz(FormularSchutz formularSchutz, KontaktLimit limit)
        {
            _formularSchutz = formularSchutz;
            _limit = limit;
        }

        /// <summary>Für das versteckte Feld beim Ausliefern des Formulars.</summary>
        public string Zeitstempel() => _formularSchutz.Zeitstempel();

        /// <summary>
        /// Prüft eine abgeschickte Nachricht. Wird sie angenommen, zählt sie für <see cref="KontaktLimit"/> mit:
        /// deshalb erst aufrufen, wenn feststeht, dass sie sonst verschickt würde.
        /// </summary>
        /// <param name="ip">Adresse des Absenders, falls bekannt.</param>
        public KontaktPruefung Pruefen(string? honeypot, string? zeitstempel, string email, string nachricht, string? ip)
        {
            if (!string.IsNullOrWhiteSpace(honeypot))
                return new(KontaktAnfrageStatus.Honeypot, null);

            if (_formularSchutz.Alter(zeitstempel) is not { } alter)
                return new(KontaktAnfrageStatus.FormularUngueltig, "Zeitstempel fehlt oder ist manipuliert");
            if (alter > FormularGueltigFuer)
                return new(KontaktAnfrageStatus.FormularUngueltig, "Zeitstempel abgelaufen");
            if (alter < FormularSchutz.Mindestdauer)
                return new(KontaktAnfrageStatus.ZuSchnell, null);

            if (NachrichtenPruefung.Beanstandung(nachricht) is { } beanstandung)
                return new(KontaktAnfrageStatus.Inhalt, beanstandung);

            if (!_limit.Annehmen(ip, email))
                return new(KontaktAnfrageStatus.ZuVieleAnfragen, null);

            return KontaktPruefung.Angenommen;
        }

        /// <summary>
        /// Was der Absender bei einer Ablehnung liest. Ein Mensch erfährt, was er tun kann;
        /// beim Honeypot steht nur die allgemeine Fehlermeldung, damit ein Bot nichts daraus lernt.
        /// </summary>
        public static string Meldung(KontaktAnfrageStatus abgelehnt) => abgelehnt switch
        {
            KontaktAnfrageStatus.ZuSchnell => "Das ging sehr schnell. Bitte warte einen Moment und sende deine Nachricht dann noch einmal.",
            KontaktAnfrageStatus.FormularUngueltig => "Das Formular war zu lange geöffnet. Bitte sende deine Nachricht noch einmal.",
            KontaktAnfrageStatus.Inhalt => $"Nachrichten mit mehreren Links oder mit HTML nehmen wir über das Formular nicht an. Bitte entferne die Links. {DirektSchreiben}",
            KontaktAnfrageStatus.ZuVieleAnfragen => $"Über das Formular sind gerade zu viele Nachrichten eingegangen. Bitte versuche es später noch einmal. {DirektSchreiben}",
            _ => $"Die Nachricht konnte leider nicht versendet werden! {DirektSchreiben}"
        };
    }

    /// <param name="Abgelehnt">null, wenn die Nachricht angenommen ist, sonst der Grund der Ablehnung.</param>
    /// <param name="Hinweis">Einzelheit für das Protokoll.</param>
    public sealed record KontaktPruefung(KontaktAnfrageStatus? Abgelehnt, string? Hinweis)
    {
        public static readonly KontaktPruefung Angenommen = new(null, null);
    }
}
