using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Components.Pages.Veranstaltungen
{
    /// <summary>Anzeigetexte, die mehrere öffentliche Seiten brauchen.</summary>
    public static class VeranstaltungTexte
    {
        /// <summary>Formular-Auswahl der aktiven Termine, vorbelegt mit den gebuchten.</summary>
        public static List<TagWahl> TagAuswahl(IEnumerable<TagAnzeige> tage, IReadOnlyCollection<int> gebucht) =>
            tage.Where(t => !t.Abgesagt).Select(t => new TagWahl { Id = t.Id, Gewaehlt = gebucht.Contains(t.Id) }).ToList();
    }
}
