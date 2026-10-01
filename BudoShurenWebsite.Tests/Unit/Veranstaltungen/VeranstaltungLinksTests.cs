using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.AspNetCore.Http;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class VeranstaltungLinksTests
{
    [Theory]
    [InlineData("/veranstaltungen/bestaetigen/geheim123", "/veranstaltungen/bestaetigen/***")]
    [InlineData("/veranstaltungen/meine-anmeldung/geheim123", "/veranstaltungen/meine-anmeldung/***")]
    [InlineData("/Veranstaltungen/Info-Abmelden/geheim123/x", "/Veranstaltungen/Info-Abmelden/***")]
    [InlineData("/veranstaltungen/benachrichtigung-abmelden/geheim", "/veranstaltungen/benachrichtigung-abmelden/***")]
    [InlineData("/veranstaltungen/herbstseminar", "/veranstaltungen/herbstseminar")]
    [InlineData("/aktuelles/irgendwas", "/aktuelles/irgendwas")]
    [InlineData(null, "")]
    public void Tokens_werden_fuer_Logs_maskiert(string? pfad, string erwartet)
    {
        VeranstaltungLinks.OhneToken(pfad).ShouldBe(erwartet);
    }

    [Fact]
    public void Links_bauen_auf_der_Basis_auf()
    {
        VeranstaltungLinks.BestaetigenUrl("https://x.de/", "abc").ShouldBe("https://x.de/veranstaltungen/bestaetigen/abc");
        VeranstaltungLinks.Veranstaltung("https://x.de/", "herbst seminar").ShouldBe("https://x.de/veranstaltungen/herbst%20seminar");
    }

    [Fact]
    public void Sicherheits_Header_fuer_Token_Seiten()
    {
        var kontext = new DefaultHttpContext();

        VeranstaltungLinks.SicherheitsHeaderSetzen(kontext.Response);

        kontext.Response.Headers["Referrer-Policy"].ToString().ShouldBe("no-referrer");
        kontext.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        kontext.Response.Headers["X-Robots-Tag"].ToString().ShouldContain("noindex");
    }
}
