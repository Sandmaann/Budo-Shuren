using Bunit;
using BudoShurenWebsite.Components.Shared.Veranstaltungen;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Services.Veranstaltungen;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace BudoShurenWebsite.Tests.Komponenten;

[Trait("Category", "Komponente")]
public class InhaltsBloeckeEditorTests : BunitContext
{
    private static readonly VerwaltungsBenutzer Admin = new("u1", "Olga", IstAdmin: true, IstAbteilungsleiter: false, Abteilung: null);

    private readonly IVeranstaltungVerwaltungService _service = Substitute.For<IVeranstaltungVerwaltungService>();
    private readonly List<BlockEingabe> _bloecke = [];

    public InhaltsBloeckeEditorTests() => Services.AddSingleton(_service);

    private IRenderedComponent<InhaltsBloeckeEditor> Editor() =>
        Render<InhaltsBloeckeEditor>(p => p
            .Add(x => x.Bloecke, _bloecke)
            .Add(x => x.Benutzer, Admin)
            .Add(x => x.AltText, "Herbstseminar"));

    private static void Klicken(IRenderedComponent<InhaltsBloeckeEditor> editor, string text) =>
        editor.FindAll("button").First(b => b.TextContent.Trim() == text).Click();

    [Fact]
    public void Text_hinzufuegen_zeigt_sichere_Vorschau()
    {
        var editor = Editor();

        Klicken(editor, "+ Text");
        editor.Find("textarea").Input("**Programm** <script>alert(1)</script>");

        _bloecke.ShouldHaveSingleItem().Typ.ShouldBe(VeranstaltungBlockTyp.MarkdownText);
        var vorschau = editor.Find(".markdown-content");
        vorschau.InnerHtml.ShouldContain("<strong>Programm</strong>");
        vorschau.QuerySelector("script").ShouldBeNull("HTML im Text wird nicht ausgeführt");
    }

    [Fact]
    public void Bausteine_verschieben_und_entfernen()
    {
        var editor = Editor();
        Klicken(editor, "+ Text");
        Klicken(editor, "+ Bilder");

        editor.Find("[data-block='1'] button[title='Nach oben']").Click();
        _bloecke.Select(b => b.Typ).ShouldBe([VeranstaltungBlockTyp.BilderGalerie, VeranstaltungBlockTyp.MarkdownText]);

        editor.Find("[data-block='0'] button[title='Entfernen']").Click();
        _bloecke.ShouldHaveSingleItem().Typ.ShouldBe(VeranstaltungBlockTyp.MarkdownText);
    }

    [Fact]
    public void Hochgeladene_Bilder_kommen_in_die_Galerie_Fehler_werden_angezeigt()
    {
        _service.BildHochladenAsync(Arg.Any<Stream>(), "dojo.png", Admin, Arg.Any<CancellationToken>()).Returns(VerwaltungsErgebnis.Ok(42));
        _service.BildHochladenAsync(Arg.Any<Stream>(), "notiz.txt", Admin, Arg.Any<CancellationToken>())
            .Returns(VerwaltungsErgebnis.MitFehler("„notiz.txt“ ist kein unterstütztes Bild (JPG, PNG oder WebP)."));
        var editor = Editor();
        Klicken(editor, "+ Bilder");

        editor.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromBinary([1, 2, 3], "dojo.png"),
            InputFileContent.CreateFromBinary([4], "notiz.txt"));

        _bloecke.Single().BildIds.ShouldBe([42]);
        editor.WaitForAssertion(() => editor.Find("img").GetAttribute("src").ShouldBe("/Account/Member/Filesave/GetImage/42"));
        editor.Markup.ShouldContain("„notiz.txt“ ist kein unterstütztes Bild");
    }
}
