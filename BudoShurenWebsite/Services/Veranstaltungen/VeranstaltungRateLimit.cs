using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Begrenzt das Absenden der öffentlichen Formulare (Anmelden, Bestätigen, Link anfordern) pro IP-Adresse.
    /// Seiten aufrufen (GET) wird nicht begrenzt. Hinter IIS (In-Process) ist RemoteIpAddress die echte Client-Adresse.
    /// </summary>
    public static class VeranstaltungRateLimit
    {
        public const string Formulare = "Veranstaltungen.Formulare";

        public const int ErlaubteAnfragen = 10;

        public static readonly TimeSpan Zeitfenster = TimeSpan.FromMinutes(10);

        public static void Konfigurieren(RateLimiterOptions optionen)
        {
            optionen.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            optionen.AddPolicy(Formulare, kontext =>
                HttpMethods.IsPost(kontext.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        kontext.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = ErlaubteAnfragen, Window = Zeitfenster, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("lesen"));
        }
    }
}
