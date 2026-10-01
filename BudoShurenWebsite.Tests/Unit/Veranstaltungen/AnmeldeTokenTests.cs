using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Tests.Unit.Veranstaltungen;

[Trait("Category", "Unit")]
public class AnmeldeTokenTests
{
    [Fact]
    public void Token_ist_urlsicher_und_der_Hash_passt()
    {
        var token = AnmeldeToken.Erzeugen();

        token.Klartext.Length.ShouldBe(43);
        token.Klartext.ShouldMatch("^[A-Za-z0-9_-]{43}$");
        token.Hash.Length.ShouldBe(32);
        AnmeldeToken.Hash(token.Klartext).ShouldBe(token.Hash);
    }

    [Fact]
    public void Tokens_sind_zufaellig()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => AnmeldeToken.Erzeugen().Klartext).ToList();

        tokens.Distinct().Count().ShouldBe(100);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zu-kurz")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // 44 Zeichen
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA!")]  // ungültiges Zeichen
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]  // Padding gehört nicht dazu
    public void Ungueltige_Tokens_ergeben_keinen_Hash(string? text)
    {
        AnmeldeToken.Hash(text).ShouldBeNull();
    }
}
