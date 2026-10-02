using System.Security.Claims;
using BudoShurenWebsite.Global;
using BudoShurenWebsite.Services.Systemzustand;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BudoShurenWebsite.Tests.Unit.Systemzustand;

[Trait("Category", "Unit")]
public class SystemzustandPruefungTests
{
    private static readonly ClaimsPrincipal Anonym = new(new ClaimsIdentity());

    private static ClaimsPrincipal Benutzer(string rolle) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "x"), new Claim(ClaimTypes.Role, rolle)], "Test"));

    [Fact]
    public void Admins_duerfen_immer()
    {
        SystemzustandPruefung.ZugriffErlaubt(Benutzer(Roles.Admin), null, null).ShouldBeTrue();
    }

    [Theory]
    [InlineData(Roles.Abteilungsleiter)]
    [InlineData(Roles.Editor)]
    [InlineData(Roles.Mitglied)]
    public void Andere_Rollen_nur_mit_Token(string rolle)
    {
        SystemzustandPruefung.ZugriffErlaubt(Benutzer(rolle), null, "geheim").ShouldBeFalse();
        SystemzustandPruefung.ZugriffErlaubt(Benutzer(rolle), "geheim", "geheim").ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, "geheim")]
    [InlineData("", "geheim")]
    [InlineData("falsch", "geheim")]
    [InlineData("geheim2", "geheim")]
    [InlineData("geheim", null)]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Anonym_ohne_passenden_Token_kein_Zugriff(string? gesendet, string? konfiguriert)
    {
        SystemzustandPruefung.ZugriffErlaubt(Anonym, gesendet, konfiguriert).ShouldBeFalse();
    }

    [Fact]
    public void Anonym_mit_passendem_Token_Zugriff()
    {
        SystemzustandPruefung.ZugriffErlaubt(Anonym, "geheim", "geheim").ShouldBeTrue();
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, StatusCodes.Status200OK)]
    [InlineData(HealthStatus.Degraded, StatusCodes.Status200OK)]
    [InlineData(HealthStatus.Unhealthy, StatusCodes.Status503ServiceUnavailable)]
    public void Nur_ungesund_liefert_503(HealthStatus status, int erwartet)
    {
        SystemzustandPruefung.StatusCode(status).ShouldBe(erwartet);
    }
}
