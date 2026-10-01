using System.Text.Encodings.Web;
using BudoShurenWebsite.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Login für HTTP-Tests ohne Cookie: Anfragen mit dem Header <see cref="Header"/> (User-Id) gelten als dieser Benutzer.
/// Die Claims baut dieselbe Fabrik wie beim echten Login (inkl. Rollen aus der Datenbank).
/// Anfragen ohne Header laufen über das normale Identity-Cookie (anonym: Weiterleitung zum Login).
/// </summary>
public sealed class TestAnmeldung(IOptionsMonitor<AuthenticationSchemeOptions> optionen, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(optionen, logger, encoder)
{
    public const string Schema = "Test";
    public const string Auswahl = "TestOderCookie";
    public const string Header = "X-Test-Benutzer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[Header].ToString();
        var userManager = Context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByIdAsync(userId) is not { } user)
            return AuthenticateResult.Fail("Unbekannter Testbenutzer");

        var fabrik = Context.RequestServices.GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var principal = await fabrik.CreateAsync(user);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Schema));
    }

    public static void Registrieren(IServiceCollection dienste)
    {
        dienste.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, TestAnmeldung>(Schema, null)
            .AddPolicyScheme(Auswahl, null, o => o.ForwardDefaultSelector = kontext =>
                kontext.Request.Headers.ContainsKey(Header) ? Schema : IdentityConstants.ApplicationScheme);
        dienste.PostConfigure<AuthenticationOptions>(o => o.DefaultScheme = Auswahl);
    }
}
