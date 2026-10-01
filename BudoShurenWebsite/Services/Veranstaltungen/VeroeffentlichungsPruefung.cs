using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Checkliste, bevor eine Veranstaltung veröffentlicht werden darf.</summary>
    public static class VeroeffentlichungsPruefung
    {
        /// <summary>Liefert alle fehlenden oder widersprüchlichen Angaben; leer = kann veröffentlicht werden.</summary>
        /// <param name="jetztOrtszeit">Aktuelle Ortszeit (Global.Ortszeit.Jetzt).</param>
        public static IReadOnlyList<string> Pruefen(Veranstaltung veranstaltung, IReadOnlyCollection<VeranstaltungsTag> tage, DateTime jetztOrtszeit)
        {
            var fehler = PruefenOhneBeginn(veranstaltung, tage).ToList();

            var ersterTag = tage.Where(t => !t.Abgesagt).OrderBy(t => t.Datum).ThenBy(t => t.Beginn).FirstOrDefault();
            if (ersterTag is not null && ersterTag.Datum.ToDateTime(ersterTag.Beginn) <= jetztOrtszeit)
                fehler.Add("Die Veranstaltung hat bereits begonnen.");

            return fehler;
        }

        /// <summary>
        /// Wie Pruefen, aber ohne "hat bereits begonnen": für das Speichern einer bereits veröffentlichten
        /// Veranstaltung, die auch während oder nach der Veranstaltung noch korrigiert werden darf.
        /// </summary>
        public static IReadOnlyList<string> PruefenOhneBeginn(Veranstaltung veranstaltung, IReadOnlyCollection<VeranstaltungsTag> tage)
        {
            var fehler = new List<string>();

            if (string.IsNullOrWhiteSpace(veranstaltung.Titel))
                fehler.Add("Titel fehlt.");
            if (string.IsNullOrWhiteSpace(veranstaltung.Slug))
                fehler.Add("Adresse (Slug) fehlt.");

            if (string.IsNullOrWhiteSpace(veranstaltung.KontaktEmail))
                fehler.Add("Kontakt-E-Mail fehlt (wird als Antwortadresse in allen Mails verwendet).");
            else if (!EmailAdresse.IstGueltig(veranstaltung.KontaktEmail))
                fehler.Add("Kontakt-E-Mail ist keine gültige E-Mail-Adresse.");

            var aktiveTage = tage.Where(t => !t.Abgesagt).OrderBy(t => t.Datum).ThenBy(t => t.Beginn).ToList();
            if (aktiveTage.Count == 0)
            {
                fehler.Add("Mindestens ein Tag muss angelegt sein.");
                return fehler;
            }

            foreach (var tag in aktiveTage.Where(t => t.Ende <= t.Beginn))
                fehler.Add($"Am {tag.Datum:dd.MM.yyyy} liegt das Ende nicht nach dem Beginn.");

            foreach (var tag in aktiveTage.Where(t => t.MaxTeilnehmer is <= 0))
                fehler.Add($"Die Kapazität am {tag.Datum:dd.MM.yyyy} muss größer als 0 sein (oder leer für unbegrenzt).");

            var beginn = aktiveTage[0].Datum.ToDateTime(aktiveTage[0].Beginn);

            if (veranstaltung.AnmeldungBis is { } bis && bis > beginn)
                fehler.Add("Der Anmeldeschluss liegt nach dem Beginn der Veranstaltung.");
            if (veranstaltung.AnmeldungAb is { } ab && veranstaltung.AnmeldungBis is { } bis2 && ab >= bis2)
                fehler.Add("Der Anmeldebeginn liegt nicht vor dem Anmeldeschluss.");
            if (veranstaltung.AenderungenBis is { } aenderungenBis && aenderungenBis > beginn)
                fehler.Add("Die Änderungsfrist liegt nach dem Beginn der Veranstaltung.");

            if (veranstaltung.MaxBegleitpersonen < 0)
                fehler.Add("Die Anzahl der Begleitpersonen darf nicht negativ sein.");

            if (veranstaltung.Teilnahmemodus == Teilnahmemodus.EinzelneTage
                && (veranstaltung.MinTageBeiTeilanmeldung < 1 || veranstaltung.MinTageBeiTeilanmeldung > aktiveTage.Count))
            {
                fehler.Add($"Die Mindestzahl an Tagen muss zwischen 1 und {aktiveTage.Count} liegen.");
            }

            return fehler;
        }
    }
}
