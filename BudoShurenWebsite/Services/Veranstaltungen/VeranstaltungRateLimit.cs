using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Begrenzt das Absenden der öffentlichen Formulare (Anmelden, Bestätigen, Link anfordern) pro IP-Adresse.
    /// Seiten aufrufen (GET) wird nicht begrenzt. Hinter IIS (In-Process) ist RemoteIpAddress die echte Client-Adresse.
    /// Abgelehnte Anfragen bekommen Status 429 und die Seite ZuVieleAnfragen (siehe <see cref="UseZuVieleAnfragenSeite"/>).
    /// </summary>
    public static class VeranstaltungRateLimit
    {
        public const string Formulare = "Veranstaltungen.Formulare";

        public const int ErlaubteAnfragen = 10;

        /// <summary>Route der Seite ZuVieleAnfragen.</summary>
        public const string HinweisSeite = "/zu-viele-anfragen";

        public static readonly TimeSpan Zeitfenster = TimeSpan.FromMinutes(10);

        public static void Konfigurieren(RateLimiterOptions optionen)
        {
            optionen.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            optionen.OnRejected = (kontext, _) =>
            {
                var anfrage = kontext.HttpContext.Request;
                TimeSpan? wartezeit = kontext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var bisZumNeuenFenster) ? bisZumNeuenFenster : null;
                if (wartezeit is { } zeit)
                    kontext.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(zeit.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                kontext.HttpContext.Features.Set(new RateLimitAblehnung(anfrage.Path + anfrage.QueryString, wartezeit));
                return ValueTask.CompletedTask;
            };
            optionen.AddPolicy(Formulare, kontext =>
                HttpMethods.IsPost(kontext.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        kontext.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = ErlaubteAnfragen, Window = Zeitfenster, QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("lesen"));
        }

        /// <summary>
        /// Füllt die leere 429-Antwort des Rate-Limiters mit der Seite ZuVieleAnfragen: Die Anfrage läuft dafür
        /// noch einmal als GET auf <see cref="HinweisSeite"/> durch die Pipeline, der Status 429 bleibt.
        /// Muss vor UseRateLimiter stehen.
        /// </summary>
        public static void UseZuVieleAnfragenSeite(this WebApplication app)
        {
            app.Use(next =>
            {
                // Das Routing läuft schon vor dieser Middleware. Für die Hinweisseite muss es noch einmal laufen,
                // deshalb ein eigener Zweig mit UseRouting (so macht es auch UseStatusCodePagesWithReExecute).
                var zweig = ((IApplicationBuilder)app).New();
                zweig.Properties["__GlobalEndpointRouteBuilder"] = app;
                zweig.UseRouting();
                zweig.Run(next);
                var mitRouting = zweig.Build();

                return async kontext =>
                {
                    await next(kontext);

                    if (kontext.Features.Get<RateLimitAblehnung>() is null || kontext.Response.HasStarted)
                        return;

                    var anfrage = kontext.Request;
                    var (methode, pfad, query) = (anfrage.Method, anfrage.Path, anfrage.QueryString);
                    var endpunkt = kontext.GetEndpoint();
                    var routenWerte = anfrage.RouteValues;

                    anfrage.Method = HttpMethods.Get;
                    anfrage.Path = HinweisSeite;
                    anfrage.QueryString = QueryString.Empty;
                    anfrage.RouteValues = new RouteValueDictionary();
                    kontext.SetEndpoint(null);
                    try
                    {
                        await mitRouting(kontext);
                    }
                    finally
                    {
                        anfrage.Method = methode;
                        anfrage.Path = pfad;
                        anfrage.QueryString = query;
                        anfrage.RouteValues = routenWerte;
                        kontext.SetEndpoint(endpunkt);
                    }
                };
            });
        }
    }

    /// <summary>Hängt an einer vom Rate-Limiter abgelehnten Anfrage (HttpContext.Features).</summary>
    /// <param name="Pfad">Ursprünglich aufgerufener Pfad mit Query, ohne PathBase.</param>
    /// <param name="Wartezeit">Zeit, bis wieder Anfragen angenommen werden; null, wenn unbekannt.</param>
    public sealed record RateLimitAblehnung(string Pfad, TimeSpan? Wartezeit);
}
