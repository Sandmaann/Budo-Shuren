using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BudoShurenWebsite.Global;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>appsettings-Abschnitt "Systemzustand".</summary>
    public sealed class SystemzustandOptionen
    {
        public const string Abschnitt = "Systemzustand";

        /// <summary>
        /// Geheimer Wert für Überwachungsdienste (z. B. BetterStack Uptime) im Header <see cref="SystemzustandPruefung.TokenHeader"/>.
        /// Leer: nur eingeloggte Admins dürfen die Prüfungen abrufen.
        /// </summary>
        public string? Token { get; set; }
    }

    /// <summary>
    /// Health Checks der Website. Zwei Gruppen:
    /// <list type="bullet">
    /// <item><see cref="Betrieb"/> (GET /health): schnell, für die regelmäßige Überwachung. Datenbank, Hintergrunddienste,
    /// Mail-Warteschlange. "Ungesund" (HTTP 503) heißt: jemand muss sich kümmern.</item>
    /// <item><see cref="Daten"/> (zusätzlich in GET /health/alle und auf der Admin-Seite): Datenprüfungen wie lose Bilder
    /// oder Kalender-Abweichungen. Melden höchstens "eingeschränkt" (HTTP 200).</item>
    /// </list>
    /// Nicht anonym: Zugriff nur für eingeloggte Admins oder mit dem Token aus <see cref="SystemzustandOptionen"/>.
    /// </summary>
    public static class SystemzustandPruefung
    {
        public const string Betrieb = "betrieb";
        public const string Daten = "daten";
        public const string TokenHeader = "X-Health-Token";

        private static readonly TimeSpan DatenbankTimeout = TimeSpan.FromSeconds(15);

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        public static IServiceCollection AddSystemzustand(this IServiceCollection dienste, IConfiguration konfiguration)
        {
            dienste.AddOptions<SystemzustandOptionen>().Bind(konfiguration.GetSection(SystemzustandOptionen.Abschnitt));
            dienste.AddSingleton<DienstHerzschlag>();
            dienste.AddHealthChecks()
                .AddCheck<DatenbankCheck>("Datenbank", tags: [Betrieb], timeout: DatenbankTimeout)
                .AddCheck<HintergrundDiensteCheck>("Hintergrunddienste", tags: [Betrieb])
                .AddCheck<MailWarteschlangeCheck>("Mail-Warteschlange", tags: [Betrieb], timeout: DatenbankTimeout)
                .AddCheck<BilderCheck>("Bilder", failureStatus: HealthStatus.Degraded, tags: [Daten], timeout: DatenbankTimeout)
                .AddCheck<VeranstaltungWartungCheck>("Veranstaltungen: Wartung", failureStatus: HealthStatus.Degraded, tags: [Daten], timeout: DatenbankTimeout)
                .AddCheck<VeranstaltungKalenderCheck>("Veranstaltungen: Kalender", failureStatus: HealthStatus.Degraded, tags: [Daten], timeout: DatenbankTimeout)
                .AddCheck<VeranstaltungAnmeldungenCheck>("Veranstaltungen: Anmeldungen", failureStatus: HealthStatus.Degraded, tags: [Daten], timeout: DatenbankTimeout);
            return dienste;
        }

        public static IEndpointRouteBuilder MapSystemzustand(this IEndpointRouteBuilder app)
        {
            // Mit den Diensten als Parametern: ein Lambda nur mit HttpContext wäre ein RequestDelegate, dessen IResult verworfen würde
            app.MapGet("/health", (HttpContext kontext, HealthCheckService pruefungen, IOptions<SystemzustandOptionen> optionen) =>
                AntwortenAsync(kontext, pruefungen, optionen.Value, nurBetrieb: true));
            app.MapGet("/health/alle", (HttpContext kontext, HealthCheckService pruefungen, IOptions<SystemzustandOptionen> optionen) =>
                AntwortenAsync(kontext, pruefungen, optionen.Value, nurBetrieb: false));
            return app;
        }

        /// <summary>Für die Admin-Seite und die Endpunkte.</summary>
        public static Task<HealthReport> PruefenAsync(HealthCheckService pruefungen, bool nurBetrieb, CancellationToken abbruch) =>
            nurBetrieb
                ? pruefungen.CheckHealthAsync(r => r.Tags.Contains(Betrieb), abbruch)
                : pruefungen.CheckHealthAsync(abbruch);

        /// <summary>Admin (eingeloggt) oder ein konfigurierter Token, der genau passt.</summary>
        public static bool ZugriffErlaubt(ClaimsPrincipal benutzer, string? gesendeterToken, string? konfigurierterToken)
        {
            if (benutzer.Identity?.IsAuthenticated == true && benutzer.IsInRole(Roles.Admin))
                return true;
            if (string.IsNullOrEmpty(konfigurierterToken) || string.IsNullOrEmpty(gesendeterToken))
                return false;
            // Vergleich in konstanter Zeit, damit die Antwortzeit nichts über den Token verrät
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(gesendeterToken), Encoding.UTF8.GetBytes(konfigurierterToken));
        }

        /// <summary>Wie bei ASP.NET üblich: gesund und eingeschränkt 200, ungesund 503.</summary>
        public static int StatusCode(HealthStatus status) =>
            status == HealthStatus.Unhealthy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK;

        public static string Text(HealthStatus status) => status switch
        {
            HealthStatus.Healthy => "gesund",
            HealthStatus.Degraded => "eingeschränkt",
            _ => "ungesund"
        };

        private static async Task<IResult> AntwortenAsync(HttpContext kontext, HealthCheckService pruefungen, SystemzustandOptionen optionen, bool nurBetrieb)
        {
            if (!ZugriffErlaubt(kontext.User, kontext.Request.Headers[TokenHeader].ToString(), optionen.Token))
                return Results.StatusCode(StatusCodes.Status401Unauthorized);

            var bericht = await PruefenAsync(pruefungen, nurBetrieb, kontext.RequestAborted);

            kontext.Response.Headers.CacheControl = "no-store";
            var inhalt = new
            {
                status = bericht.Status.ToString(),
                text = Text(bericht.Status),
                version = AppVersion.Text,
                dauerMs = (int)bericht.TotalDuration.TotalMilliseconds,
                pruefungen = bericht.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    // Bei einer Ausnahme ist die Beschreibung deren Text (kann Server- oder Verbindungsdetails enthalten): nicht ausgeben, steht im Log
                    beschreibung = e.Value.Exception is not null ? $"Fehler bei der Prüfung ({e.Value.Exception.GetType().Name}), siehe Log." : e.Value.Description,
                    dauerMs = (int)e.Value.Duration.TotalMilliseconds,
                    daten = e.Value.Data.Count > 0 ? e.Value.Data : null
                })
            };
            return Results.Json(inhalt, Json, statusCode: StatusCode(bericht.Status));
        }
    }
}
