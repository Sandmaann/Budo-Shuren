using BudoShurenWebsite.Global;

namespace BudoShurenWebsite.Tests.Unit.Global;

[Trait("Category", "Unit")]
public class MarkdownTextTests
{
    [Fact]
    public void Markdown_wird_zu_HTML()
    {
        MarkdownText.SicherZuHtml("**Wichtig:** bitte *pünktlich*").ShouldContain("<strong>Wichtig:</strong>");
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("Text <iframe src=\"https://boese.example\"></iframe>")]
    public void Eingebettetes_HTML_wird_nicht_ausgefuehrt(string markdown)
    {
        var html = MarkdownText.SicherZuHtml(markdown);

        html.ShouldNotContain("<script");
        html.ShouldNotContain("<img");
        html.ShouldNotContain("<iframe");
        html.ShouldContain("&lt;");
    }

    [Fact]
    public void Leerer_Text()
    {
        MarkdownText.SicherZuHtml(null).ShouldBe(string.Empty);
    }
}
