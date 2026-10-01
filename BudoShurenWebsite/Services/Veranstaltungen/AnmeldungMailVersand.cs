using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Reiht Mails zu einer Anmeldung in die Warteschlange ein (im DbContext des Aufrufers, siehe IEmailWarteschlange).
    /// Antwortadresse ist immer der Kontakt der Veranstaltung.
    /// </summary>
    public sealed class AnmeldungMailVersand
    {
        public const string BezugTyp = "Anmeldung";

        private readonly IEmailWarteschlange _warteschlange;

        public AnmeldungMailVersand(IEmailWarteschlange warteschlange)
        {
            _warteschlange = warteschlange;
        }

        /// <param name="an">Abweichende Adresse, z. B. die neue Adresse beim E-Mail-Wechsel; sonst die der Anmeldung.</param>
        public void AnTeilnehmer(ApplicationDbContext kontext, Veranstaltung v, Anmeldung anmeldung, MailInhalt inhalt,
            EmailPrioritaet prioritaet = EmailPrioritaet.Hoch, string? an = null) =>
            _warteschlange.Hinzufuegen(kontext, new AusgehendeEmail(an ?? anmeldung.Email, inhalt.Betreff, inhalt.Html)
            {
                AntwortAn = v.KontaktEmail,
                Prioritaet = prioritaet,
                BezugTyp = BezugTyp,
                BezugId = anmeldung.Id == 0 ? null : anmeldung.Id
            });

        /// <summary>
        /// Kurze Info an Info-Adressen (nicht abgemeldete), jeweils mit neuem Abmeldelink.
        /// </summary>
        /// <param name="nurAdressen">Nur diese Adressen (z. B. neu hinzugekommene); null = alle.</param>
        public void InfoMails(ApplicationDbContext kontext, Veranstaltung v, IReadOnlyCollection<VeranstaltungsTag> tage, Anmeldung anmeldung,
            string basisUrl, IReadOnlyCollection<string>? nurAdressen = null)
        {
            foreach (var info in anmeldung.InfoEmails.Where(i => i.AbgemeldetUtc is null && (nurAdressen is null || nurAdressen.Contains(i.Email))))
            {
                var abmelden = AnmeldeToken.Erzeugen();
                info.AbmeldeTokenHash = abmelden.Hash;
                var inhalt = VeranstaltungMailVorlagen.InfoAnBegleitung(
                    v, tage, anmeldung, VeranstaltungLinks.Veranstaltung(basisUrl, v.Slug), VeranstaltungLinks.InfoAbmeldenUrl(basisUrl, abmelden.Klartext));
                _warteschlange.Hinzufuegen(kontext, new AusgehendeEmail(info.Email, inhalt.Betreff, inhalt.Html)
                {
                    AntwortAn = v.KontaktEmail,
                    Prioritaet = EmailPrioritaet.Normal,
                    BezugTyp = BezugTyp,
                    BezugId = anmeldung.Id
                });
            }
        }

        /// <summary>Mail ohne Bezug zu einer einzelnen Veranstaltung (z. B. alle Links einer Adresse), ohne Antwortadresse.</summary>
        public void Allgemein(ApplicationDbContext kontext, string an, MailInhalt inhalt) =>
            _warteschlange.Hinzufuegen(kontext, new AusgehendeEmail(an, inhalt.Betreff, inhalt.Html) { Prioritaet = EmailPrioritaet.Hoch });

        public void VersandAnstossen() => _warteschlange.VersandAnstossen();
    }
}
