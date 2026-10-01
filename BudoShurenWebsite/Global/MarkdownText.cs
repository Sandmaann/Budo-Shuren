using Markdig;

namespace BudoShurenWebsite.Global
{
    public static class MarkdownText
    {
        // Wie in den Block-Renderern, aber ohne eingebettetes HTML: Texte aus der Verwaltung
        // erscheinen auf öffentlichen Seiten und dürfen kein Script einschleusen können.
        private static readonly MarkdownPipeline SicherePipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoLinks()
            .DisableHtml()
            .Build();

        /// <summary>Markdown als HTML; HTML im Text wird als Text ausgegeben, nicht ausgeführt.</summary>
        public static string SicherZuHtml(string? markdown) => Markdown.ToHtml(markdown ?? string.Empty, SicherePipeline);
    }
}
