using System.Reflection;

namespace BudoShurenWebsite.Global
{
    /// <summary>
    /// Version der Website, wie sie in der .csproj als FileVersion steht (z. B. "2.1.0.0").
    /// Angezeigt im Impressum und auf der Admin-Seite, damit nach einem Deploy prüfbar ist, welcher Stand läuft.
    /// </summary>
    public static class AppVersion
    {
        public static string Text { get; } =
            typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? typeof(AppVersion).Assembly.GetName().Version?.ToString()
            ?? "unbekannt";
    }
}
