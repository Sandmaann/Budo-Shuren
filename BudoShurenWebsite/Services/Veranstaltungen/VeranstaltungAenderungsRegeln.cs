using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Geplante Änderung an einem bestehenden Termin.</summary>
    /// <param name="Bezeichnung">Für Meldungen, z. B. "Sa 14.11." (TerminText.Kurz).</param>
    /// <param name="Entfernen">Termin soll gelöscht werden.</param>
    /// <param name="NeuesMax">Neue Kapazität (null = unbegrenzt).</param>
    /// <param name="Belegt">Aktuell belegte Plätze bei diesem Termin.</param>
    /// <param name="AktiveAnmeldungen">Aktive Anmeldungen, die für diesen Termin gelten.</param>
    public sealed record TagAenderung(int TagId, string Bezeichnung, bool Entfernen, int? NeuesMax, int Belegt, int AktiveAnmeldungen);

    /// <summary>Geplante Änderung an einer Veranstaltung, mit dem für die Regeln nötigen Ist-Stand.</summary>
    public sealed record VeranstaltungAenderung(
        Teilnahmemodus AlterModus,
        Teilnahmemodus NeuerModus,
        string AlterSlug,
        string NeuerSlug,
        bool WarVeroeffentlicht,
        int AktiveAnmeldungen,
        IReadOnlyList<TagAenderung> Tage);

    /// <summary>
    /// Was an einer Veranstaltung nicht mehr geändert werden darf, sobald es Anmeldungen gibt
    /// bzw. sobald sie veröffentlicht war (Plan Abschnitt 6.2).
    /// </summary>
    public static class VeranstaltungAenderungsRegeln
    {
        /// <summary>Liefert alle Verstöße als verständliche Meldungen; leer = Änderung erlaubt.</summary>
        public static IReadOnlyList<string> Pruefen(VeranstaltungAenderung aenderung)
        {
            var fehler = new List<string>();

            if (aenderung.AlterModus != aenderung.NeuerModus && aenderung.AktiveAnmeldungen > 0)
                fehler.Add("Der Teilnahmemodus kann nicht mehr geändert werden, weil es bereits Anmeldungen gibt.");

            if (aenderung.WarVeroeffentlicht && !string.Equals(aenderung.AlterSlug, aenderung.NeuerSlug, StringComparison.Ordinal))
                fehler.Add("Die Adresse (Slug) kann nach der ersten Veröffentlichung nicht mehr geändert werden, weil bereits Links verschickt wurden.");

            foreach (var tag in aenderung.Tage)
            {
                if (tag.Entfernen && tag.AktiveAnmeldungen > 0)
                    fehler.Add($"Der Termin {tag.Bezeichnung} hat Anmeldungen und kann nicht gelöscht werden. Bitte den Termin stattdessen absagen.");
                else if (!tag.Entfernen && tag.NeuesMax is { } max && max < tag.Belegt)
                    fehler.Add($"Die Kapazität für {tag.Bezeichnung} kann nicht unter die aktuelle Belegung ({tag.Belegt} Personen) gesenkt werden.");
            }

            return fehler;
        }
    }
}
