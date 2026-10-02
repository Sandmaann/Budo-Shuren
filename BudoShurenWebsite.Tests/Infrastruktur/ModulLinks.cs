using System.Text.RegularExpressions;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>
/// Links des Moduls müssen relativ zum &lt;base href&gt; (appsettings "BaseHref") sein. Ein führendes "/" ignoriert
/// das BaseHref; läuft die Seite in einem Unterverzeichnis, zeigt so ein Link ins Leere.
/// Layout und ältere Seiten (Menü, Fußzeile) sind hier bewusst nicht erfasst.
/// </summary>
public static partial class ModulLinks
{
    [GeneratedRegex("""(?:href|src)="/(?:veranstaltungen|Account/Member/(?:Veranstaltungen|Filesave))[^"]*"|href="#""", RegexOptions.IgnoreCase)]
    private static partial Regex AbsoluterModulLink();

    public static void SollenRelativSein(string html) =>
        AbsoluterModulLink().Matches(html).Select(m => m.Value).ShouldBeEmpty("Links mit führendem / oder nur #anker ignorieren das BaseHref");
}
