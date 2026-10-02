using BudoShurenWebsite.Models;

namespace BudoShurenWebsite.Services
{
    public interface IAktuellesService
    {
        /// <summary>
        /// Liefert Beiträge mit Filterung und Pagination (nur veröffentlichte).
        /// </summary>
        /// <param name="skip">Anzahl zu überspringender Einträge</param>
        /// <param name="take">Anzahl zu ladender Einträge</param>
        /// <param name="suchtext">Suchtext für Titel-Filterung (optional)</param>
        /// <param name="abteilungId">Abteilungs-Filter (optional, null = alle)</param>
        /// <returns>Tuple mit Beiträgen und Gesamtanzahl</returns>
        Task<(List<AktuellesBeitrag> items, int total)> GetBeitraege(int skip, int take, string? suchtext, string? abteilungId);

        /// <summary>
        /// Liefert einen Beitrag per Slug (inkl. Blöcke und Bilder, nur wenn veröffentlicht).
        /// </summary>
        Task<AktuellesBeitrag?> GetBeitragBySlug(string slug);

        /// <summary>
        /// Liefert einen Beitrag per ID (inkl. Blöcke und Bilder, auch unveröffentlicht, für Member-Bereich).
        /// </summary>
        Task<AktuellesBeitrag?> GetById(int id);

        /// <summary>
        /// Speichert einen Beitrag (neu oder Update).
        /// Übernimmt neu hochgeladene Bilder und löscht die Bilddaten entfernter Bilder.
        /// </summary>
        Task Speichern(AktuellesBeitrag beitrag);

        /// <summary>
        /// Löscht einen Beitrag samt seinen Bilddaten (sofern nicht anderswo verwendet).
        /// </summary>
        Task Loeschen(int id);

        /// <summary>
        /// Prüft ob ein Slug bereits existiert (mit Option, einen bestimmten Beitrag auszuschließen).
        /// </summary>
        Task<bool> SlugExists(string slug, int? excludeId = null);
    }
}
