using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Services.Systemzustand
{
    /// <summary>
    /// Laufen alle Hintergrunddienste? Ausfall oder Hänger (kein Durchlauf in der erwarteten Zeit) ist "ungesund",
    /// ein Durchlauf mit Fehler oder ein ungewollt abgeschalteter Dienst "eingeschränkt".
    /// </summary>
    public sealed class HintergrundDiensteCheck : IHealthCheck
    {
        private readonly DienstHerzschlag _herzschlag;
        private readonly TimeProvider _zeit;

        public HintergrundDiensteCheck(DienstHerzschlag herzschlag, TimeProvider zeit)
        {
            _herzschlag = herzschlag;
            _zeit = zeit;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var jetzt = _zeit.GetUtcNow();
            var bewertungen = DienstHerzschlag.Erwartet
                .Select(name => (Name: name, Bewertung: DienstHerzschlag.Bewerten(_herzschlag.Stand(name), jetzt)))
                .ToList();

            // HealthStatus: Unhealthy < Degraded < Healthy, der schlechteste Dienst bestimmt das Ergebnis
            var status = bewertungen.Min(b => b.Bewertung.Status);
            var text = string.Join("; ", bewertungen.Select(b => $"{b.Name}: {b.Bewertung.Text}"));
            return Task.FromResult(new HealthCheckResult(status, text));
        }
    }
}
