using BudoShurenWebsite.Models.Enums;
using System.ComponentModel;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    public enum TeilnehmerFilterArt
    {
        [Description("Aktiv")]
        Aktiv,

        [Description("Neu/geändert")]
        Neu,

        [Description("Unbestätigt")]
        Unbestaetigt,

        [Description("Abgemeldet")]
        Abgemeldet,

        [Description("Abgelehnt")]
        Abgelehnt,

        [Description("Alle")]
        Alle
    }

    /// <summary>Schnellfilter und Suche der Teilnehmertabelle auf der Übersichtsseite.</summary>
    public static class TeilnehmerFilter
    {
        public static bool Passt(TeilnehmerZeile t, TeilnehmerFilterArt art) => art switch
        {
            TeilnehmerFilterArt.Aktiv => AnmeldungStatusUebergaenge.IstAktiv(t.Status),
            TeilnehmerFilterArt.Neu => t.HatUngeseheneAenderungen,
            TeilnehmerFilterArt.Unbestaetigt => t.Status == AnmeldungStatus.Unbestaetigt,
            TeilnehmerFilterArt.Abgemeldet => t.Status == AnmeldungStatus.Storniert,
            TeilnehmerFilterArt.Abgelehnt => t.Status == AnmeldungStatus.Abgelehnt,
            _ => true
        };

        /// <summary>Suche in Name, E-Mail und Verein (ohne Groß-/Kleinschreibung); sortiert nach Nachname, Vorname.</summary>
        public static IReadOnlyList<TeilnehmerZeile> Anwenden(IEnumerable<TeilnehmerZeile> teilnehmer, TeilnehmerFilterArt art, string? suche)
        {
            var begriff = suche?.Trim();
            return teilnehmer
                .Where(t => Passt(t, art))
                .Where(t => string.IsNullOrEmpty(begriff)
                    || $"{t.Vorname} {t.Nachname}".Contains(begriff, StringComparison.OrdinalIgnoreCase)
                    || t.Email.Contains(begriff, StringComparison.OrdinalIgnoreCase)
                    || (t.Verein?.Contains(begriff, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(t => t.Nachname, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(t => t.Vorname, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(t => t.Id)
                .ToList();
        }

        public static int Anzahl(IEnumerable<TeilnehmerZeile> teilnehmer, TeilnehmerFilterArt art) => teilnehmer.Count(t => Passt(t, art));
    }
}
