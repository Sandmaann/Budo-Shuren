namespace BudoShurenWebsite.Services.Kontakt
{
    /// <summary>
    /// Begrenzt, wie viele Nachrichten das Kontaktformular in einem Zeitfenster annimmt: je Absender (IP-Adresse
    /// und E-Mail-Adresse) und insgesamt. Das Formular wird teils über die Blazor-Verbindung abgeschickt und nicht
    /// als HTTP-Anfrage, deshalb greift der Rate-Limiter der Pipeline (VeranstaltungRateLimit) hier nicht.
    /// Der Stand liegt nur im Speicher und beginnt nach einem Neustart von vorn.
    /// </summary>
    public sealed class KontaktLimit
    {
        public const int JeAbsender = 3;

        /// <summary>Obergrenze für alle Absender zusammen: so bleibt auch bei wechselnden Adressen die Zahl der Mails klein.</summary>
        public const int Insgesamt = 20;

        public static readonly TimeSpan Zeitfenster = TimeSpan.FromHours(1);

        private readonly TimeProvider _zeit;
        private readonly object _sperre = new();
        // Höchstens "Insgesamt" Einträge: abgelehnte Nachrichten werden nicht gemerkt
        private readonly List<(DateTimeOffset Zeitpunkt, string? Ip, string Email)> _angenommen = [];

        public KontaktLimit(TimeProvider zeit)
        {
            _zeit = zeit;
        }

        /// <summary>Zählt die Nachricht mit, wenn sie noch im Rahmen liegt.</summary>
        /// <param name="ip">null, wenn die Adresse nicht bekannt ist; dann gelten nur die anderen beiden Grenzen.</param>
        /// <returns>false, wenn eine der Grenzen erreicht ist.</returns>
        public bool Annehmen(string? ip, string email)
        {
            var jetzt = _zeit.GetUtcNow();
            email = email.Trim();

            lock (_sperre)
            {
                _angenommen.RemoveAll(a => jetzt - a.Zeitpunkt >= Zeitfenster);

                if (_angenommen.Count >= Insgesamt
                    || _angenommen.Count(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase)) >= JeAbsender
                    || (ip is not null && _angenommen.Count(a => a.Ip == ip) >= JeAbsender))
                {
                    return false;
                }

                _angenommen.Add((jetzt, ip, email));
                return true;
            }
        }
    }
}
