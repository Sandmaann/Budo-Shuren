using BudoShurenWebsite.Services.Kontakt;

namespace BudoShurenWebsite.Tests.Unit.Kontakt;

[Trait("Category", "Unit")]
public class NachrichtenPruefungTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Hallo, ich würde gerne am Donnerstag zum Probetraining kommen. Was muss ich mitbringen?")]
    [InlineData("Ich habe früher hier trainiert: https://www.example.org/dojo – passt das zu euch?")]
    [InlineData("Mehrzeilig\r\nmit 武神館 und einer Frage zu z.B. Preisen (ca. 30 €).")]
    public void Gewoehnliche_Nachrichten_sind_unauffaellig(string? text)
    {
        NachrichtenPruefung.Beanstandung(text).ShouldBeNull();
    }

    [Theory]
    [InlineData("Billige Uhren http://a.example und https://b.example", "2 Links")]
    [InlineData("Schau mal: www.a.example, WWW.b.example und http://c.example", "3 Links")]
    public void Mehrere_Links_werden_beanstandet(string text, string grund)
    {
        NachrichtenPruefung.Beanstandung(text).ShouldBe(grund);
    }

    [Theory]
    [InlineData("Klick <a href=\"https://spam.example\">hier</a>")]
    [InlineData("Klick < A HREF=x>hier")]
    [InlineData("Angebot [url=https://spam.example]hier[/url]")]
    [InlineData("Angebot [URL]https://spam.example[/URL]")]
    [InlineData("Angebot [link=https://spam.example]hier[/link]")]
    public void Links_als_HTML_oder_BBCode_werden_beanstandet(string text)
    {
        NachrichtenPruefung.Beanstandung(text).ShouldBe("Link als HTML oder BBCode");
    }
}
