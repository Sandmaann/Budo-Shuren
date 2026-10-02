using BudoShurenWebsite.Services.Systemzustand;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace BudoShurenWebsite.Tests.Unit.Systemzustand;

[Trait("Category", "Unit")]
public class DienstHerzschlagTests
{
    private const string Dienst = "Testdienst";
    private static readonly TimeSpan MaxAbstand = TimeSpan.FromMinutes(10);

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero));
    private readonly DienstHerzschlag _herzschlag;

    public DienstHerzschlagTests()
    {
        _herzschlag = new DienstHerzschlag(_zeit);
    }

    private HealthStatus Status() => DienstHerzschlag.Bewerten(_herzschlag.Stand(Dienst), _zeit.GetUtcNow()).Status;

    [Fact]
    public void Nie_gemeldeter_Dienst_ist_ungesund()
    {
        Status().ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Gerade_gestartet_ohne_Durchlauf_ist_gesund_bis_der_Abstand_ueberschritten_ist()
    {
        _herzschlag.Gestartet(Dienst, MaxAbstand);
        _zeit.Advance(MaxAbstand);
        Status().ShouldBe(HealthStatus.Healthy);

        _zeit.Advance(TimeSpan.FromSeconds(1));
        Status().ShouldBe(HealthStatus.Unhealthy, "der erste Durchlauf hängt");
    }

    [Fact]
    public void Jeder_Durchlauf_setzt_die_Frist_neu()
    {
        _herzschlag.Gestartet(Dienst, MaxAbstand);
        _zeit.Advance(TimeSpan.FromMinutes(8));
        _herzschlag.Gelaufen(Dienst, ok: true);
        _zeit.Advance(TimeSpan.FromMinutes(8));

        Status().ShouldBe(HealthStatus.Healthy);

        _zeit.Advance(TimeSpan.FromMinutes(3));
        Status().ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public void Durchlauf_mit_Fehler_ist_eingeschraenkt_der_naechste_erfolgreiche_wieder_gesund()
    {
        _herzschlag.Gestartet(Dienst, MaxAbstand);
        _herzschlag.Gelaufen(Dienst, ok: false);
        Status().ShouldBe(HealthStatus.Degraded);

        _herzschlag.Gelaufen(Dienst, ok: true);
        Status().ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public void Abgeschaltet_ist_nur_eingeschraenkt_wenn_nicht_gewollt()
    {
        _herzschlag.Abgeschaltet(Dienst, "Modul aus", erwartet: true);
        _zeit.Advance(TimeSpan.FromDays(1));
        Status().ShouldBe(HealthStatus.Healthy);

        _herzschlag.Abgeschaltet(Dienst, "Schalter aus", erwartet: false);
        Status().ShouldBe(HealthStatus.Degraded);
        DienstHerzschlag.Bewerten(_herzschlag.Stand(Dienst), _zeit.GetUtcNow()).Text.ShouldContain("Schalter aus");
    }

    [Fact]
    public async Task Check_meldet_alle_erwarteten_Dienste_und_den_schlechtesten_Status()
    {
        _herzschlag.Gestartet(DienstHerzschlag.EmailVersand, MaxAbstand);
        _herzschlag.Gelaufen(DienstHerzschlag.EmailVersand, ok: true);
        _herzschlag.Abgeschaltet(DienstHerzschlag.Veranstaltungen, "Modul aus", erwartet: true);
        // BildAufraeumen hat sich nie gemeldet

        var ergebnis = await new HintergrundDiensteCheck(_herzschlag, _zeit).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        ergebnis.Status.ShouldBe(HealthStatus.Unhealthy);
        ergebnis.Description.ShouldNotBeNull();
        ergebnis.Description.ShouldContain($"{DienstHerzschlag.BildAufraeumen}: nicht gestartet");
        ergebnis.Description.ShouldContain($"{DienstHerzschlag.EmailVersand}: letzter Durchlauf");
        ergebnis.Description.ShouldContain($"{DienstHerzschlag.Veranstaltungen}: abgeschaltet (Modul aus)");
    }
}
