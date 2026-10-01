using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Mail
{
    /// <summary>Regeln für erneute Versandversuche nach einem Fehler.</summary>
    public static class EmailWiederholung
    {
        public const int MaxFehlerLaenge = 2000;

        // Wartezeit nach dem 1., 2., 3. und ab dem 4. Fehlversuch
        private static readonly TimeSpan[] Wartezeiten =
        [
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(60)
        ];

        public static TimeSpan Wartezeit(int versuche) =>
            Wartezeiten[Math.Clamp(versuche, 1, Wartezeiten.Length) - 1];

        /// <summary>Zählt den Fehlversuch und plant den nächsten Versuch oder markiert die Mail als endgültig fehlgeschlagen.</summary>
        public static void FehlschlagVermerken(EmailAusgang mail, string fehler, DateTime jetztUtc, int maxVersuche)
        {
            mail.Versuche++;
            mail.LetzterFehler = fehler.Length > MaxFehlerLaenge ? fehler[..MaxFehlerLaenge] : fehler;

            if (mail.Versuche >= maxVersuche)
                mail.Status = EmailStatus.Fehlgeschlagen;
            else
                mail.FaelligAbUtc = jetztUtc + Wartezeit(mail.Versuche);
        }
    }
}
