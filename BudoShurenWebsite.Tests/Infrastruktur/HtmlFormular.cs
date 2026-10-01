using System.Net;
using System.Text.RegularExpressions;

namespace BudoShurenWebsite.Tests.Infrastruktur;

/// <summary>Liest Formulare aus gerenderten Seiten (statisches Rendern), um sie in HTTP-Tests wie ein Browser abzuschicken.</summary>
public static class HtmlFormular
{
    /// <summary>Versteckte Felder (inkl. Antiforgery und _handler) des Formulars mit dem angegebenen FormName.</summary>
    public static Dictionary<string, string> VersteckteFelder(string html, string formName) =>
        Felder(Formular(html, formName), nurVersteckte: true);

    /// <summary>Alle Eingabefelder mit Wert (versteckte, Text, Zahl) des Formulars, wie sie vorbelegt sind.</summary>
    public static Dictionary<string, string> AlleFelder(string html, string formName)
    {
        var formular = Formular(html, formName);
        var felder = Felder(formular, nurVersteckte: false);
        foreach (Match m in Regex.Matches(formular, "<textarea\\b[^>]*name=\"([^\"]*)\"[^>]*>(.*?)</textarea>", RegexOptions.Singleline))
            felder[WebUtility.HtmlDecode(m.Groups[1].Value)] = WebUtility.HtmlDecode(m.Groups[2].Value);
        return felder;
    }

    private static string Formular(string html, string formName) =>
        Regex.Matches(html, "<form\\b.*?</form>", RegexOptions.Singleline)
            .Select(m => m.Value)
            .Single(f => f.Contains($"name=\"_handler\" value=\"{formName}\""));

    private static Dictionary<string, string> Felder(string formular, bool nurVersteckte) =>
        Regex.Matches(formular, "<input\\b[^>]*>")
            .Select(m => m.Value)
            .Where(tag => !nurVersteckte || tag.Contains("type=\"hidden\""))
            .Where(tag => !tag.Contains("type=\"checkbox\"") || tag.Contains("checked"))
            .Select(tag => (Name: Regex.Match(tag, "name=\"([^\"]*)\"").Groups[1].Value, Wert: Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value))
            .Where(f => f.Name.Length > 0)
            .GroupBy(f => f.Name)
            .ToDictionary(g => WebUtility.HtmlDecode(g.Key), g => WebUtility.HtmlDecode(g.Last().Wert));
}
