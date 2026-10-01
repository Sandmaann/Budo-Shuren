using BudoShurenWebsite.Services.Veranstaltungen;
using System.Globalization;

namespace BudoShurenWebsite.Components.Pages.Veranstaltungen
{
    /// <summary>Anzeigetexte, die mehrere öffentliche Seiten brauchen.</summary>
    public static class VeranstaltungTexte
    {
        private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

        /// <summary>"Samstag, 14.11.2026, 10:00–16:00 Uhr (Titel)".</summary>
        public static string Tag(TagAnzeige tag) =>
            $"{tag.Datum.ToString("dddd, dd.MM.yyyy", Deutsch)}, {tag.Beginn:HH\\:mm}–{tag.Ende:HH\\:mm} Uhr" +
            (string.IsNullOrWhiteSpace(tag.Titel) ? "" : $" ({tag.Titel})");

        /// <summary>Formular-Auswahl der aktiven Tage, vorbelegt mit den gebuchten.</summary>
        public static List<TagWahl> TagAuswahl(IEnumerable<TagAnzeige> tage, IReadOnlyCollection<int> gebucht) =>
            tage.Where(t => !t.Abgesagt).Select(t => new TagWahl { Id = t.Id, Gewaehlt = gebucht.Contains(t.Id) }).ToList();
    }
}
