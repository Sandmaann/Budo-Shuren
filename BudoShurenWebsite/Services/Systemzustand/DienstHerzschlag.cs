using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>Letzter bekannter Stand eines Hintergrunddienstes.</summary>
    /// <param name="MaxAbstand">So lange darf es höchstens dauern, bis der nächste Durchlauf gemeldet wird.</param>
    /// <param name="Abgeschaltet">Grund, wenn der Dienst laut Konfiguration nicht läuft; sonst null.</param>
    /// <param name="AbgeschaltetErwartet">Abschalten ist gewollt (z. B. Modul aus) und kein Warnzeichen.</param>
    public sealed record DienstStand(
        string Name,
        TimeSpan MaxAbstand,
        DateTimeOffset GestartetUtc,
        DateTimeOffset? LetzterLaufUtc,
        bool LetzterLaufOk,
        string? Abgeschaltet,
        bool AbgeschaltetErwartet);

    /// <summary>
    /// Die Hintergrunddienste melden hier ihren Start und jeden Durchlauf. Der HintergrundDiensteCheck erkennt daran
    /// Dienste, die hängen, abgestürzt sind oder wiederholt Fehler haben. Nur im Speicher: nach einem Neustart zählt
    /// der Start als letzter Lebenszeichen-Zeitpunkt.
    /// </summary>
    public sealed class DienstHerzschlag
    {
        public const string EmailVersand = "Mailversand";
        public const string Veranstaltungen = "Veranstaltungen (Benachrichtigungen, Wartung)";
        public const string BildAufraeumen = "Bilder aufräumen";

        /// <summary>Diese Dienste müssen sich nach dem Start gemeldet haben (gestartet oder abgeschaltet).</summary>
        public static readonly IReadOnlyList<string> Erwartet = [EmailVersand, Veranstaltungen, BildAufraeumen];

        private readonly ConcurrentDictionary<string, DienstStand> _staende = new();
        private readonly TimeProvider _zeit;

        public DienstHerzschlag(TimeProvider zeit)
        {
            _zeit = zeit;
        }

        public void Gestartet(string name, TimeSpan maxAbstand) =>
            _staende[name] = new DienstStand(name, maxAbstand, _zeit.GetUtcNow(), null, true, null, false);

        public void Abgeschaltet(string name, string grund, bool erwartet) =>
            _staende[name] = new DienstStand(name, TimeSpan.Zero, _zeit.GetUtcNow(), null, true, grund, erwartet);

        public void Gelaufen(string name, bool ok)
        {
            var jetzt = _zeit.GetUtcNow();
            _staende.AddOrUpdate(name,
                _ => new DienstStand(name, TimeSpan.Zero, jetzt, jetzt, ok, null, false),
                (_, s) => s with { LetzterLaufUtc = jetzt, LetzterLaufOk = ok });
        }

        public DienstStand? Stand(string name) => _staende.GetValueOrDefault(name);

        /// <summary>Bewertet einen Dienst; null = Dienst hat sich nie gemeldet.</summary>
        public static (HealthStatus Status, string Text) Bewerten(DienstStand? stand, DateTimeOffset jetzt)
        {
            if (stand is null)
                return (HealthStatus.Unhealthy, "nicht gestartet");
            if (stand.Abgeschaltet is not null)
                return (stand.AbgeschaltetErwartet ? HealthStatus.Healthy : HealthStatus.Degraded, $"abgeschaltet ({stand.Abgeschaltet})");

            var lebenszeichen = stand.LetzterLaufUtc ?? stand.GestartetUtc;
            var seit = jetzt - lebenszeichen;
            if (seit > stand.MaxAbstand)
                return (HealthStatus.Unhealthy, $"kein Durchlauf seit {Dauer(seit)} (erwartet spätestens alle {Dauer(stand.MaxAbstand)})");
            if (stand.LetzterLaufUtc is null)
                return (HealthStatus.Healthy, $"gestartet vor {Dauer(seit)}, noch kein Durchlauf abgeschlossen");
            return stand.LetzterLaufOk
                ? (HealthStatus.Healthy, $"letzter Durchlauf vor {Dauer(seit)}")
                : (HealthStatus.Degraded, $"letzter Durchlauf vor {Dauer(seit)} mit Fehler (siehe Log)");
        }

        private static string Dauer(TimeSpan d) =>
            d.TotalHours >= 1 ? $"{(int)d.TotalHours} h {d.Minutes} min"
            : d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes} min"
            : $"{Math.Max(0, (int)d.TotalSeconds)} s";
    }
}
