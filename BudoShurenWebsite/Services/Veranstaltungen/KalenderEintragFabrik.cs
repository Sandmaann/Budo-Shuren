using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Bildet Veranstaltungstage auf Einträge im bestehenden Kalender (AppointmentData) ab.
    /// Die Einträge sind über AppointmentData.VeranstaltungsTagId verknüpft und im Kalender schreibgeschützt.
    /// </summary>
    public static class KalenderEintragFabrik
    {
        public const string Ersteller = "Veranstaltungen";
        public const string AbteilungGesamtverein = "Verein";

        /// <summary>
        /// Öffentliche Veranstaltungen stehen ab der Veröffentlichung im Kalender und bleiben danach als
        /// Rückblick stehen (abgeschlossen, archiviert). Entwürfe, abgesagte Veranstaltungen und abgesagte Tage nicht,
        /// ebenso wenig archivierte Entwürfe, die nie veröffentlicht waren.
        /// </summary>
        public static bool GehoertInDenKalender(Veranstaltung veranstaltung, VeranstaltungsTag tag) =>
            veranstaltung.Sichtbarkeit == VeranstaltungSichtbarkeit.Oeffentlich
            && (veranstaltung.Status == VeranstaltungStatus.Veroeffentlicht
                || veranstaltung.Status is VeranstaltungStatus.Abgeschlossen or VeranstaltungStatus.Archiviert && veranstaltung.ErstmalsVeroeffentlichtUtc is not null)
            && !tag.Abgesagt;

        /// <summary>Überträgt die Daten der Veranstaltung und des Tages auf den Kalendereintrag.</summary>
        /// <param name="jetztOrtszeit">Für die Änderungszeitpunkte; der Kalender speichert Ortszeit.</param>
        public static void Uebernehmen(AppointmentData eintrag, Veranstaltung veranstaltung, VeranstaltungsTag tag, DateTime jetztOrtszeit)
        {
            eintrag.VeranstaltungsTagId = tag.Id;
            eintrag.Subject = string.IsNullOrWhiteSpace(tag.Titel) ? veranstaltung.Titel : $"{veranstaltung.Titel} – {tag.Titel}";
            eintrag.Location = veranstaltung.Ort ?? string.Empty;
            eintrag.StartTime = tag.Datum.ToDateTime(tag.Beginn);
            eintrag.EndTime = Ende(tag);
            eintrag.IsAllDay = false;
            eintrag.RecurrenceRule = string.Empty;
            eintrag.RecurrenceException = string.Empty;
            eintrag.RecurrenceID = null;
            // Der Link zur Veranstaltung steht im Kalender selbst (Kalender.razor), nicht als Text in der Beschreibung
            eintrag.Description = veranstaltung.Kurzbeschreibung ?? string.Empty;
            eintrag.Abteilung = veranstaltung.Abteilung?.Name ?? veranstaltung.AbteilungId ?? AbteilungGesamtverein;
            eintrag.ShowInWeek = true;
            eintrag.ShowInMonth = true;

            if (eintrag.Id == 0)
            {
                eintrag.Created = jetztOrtszeit;
                eintrag.EntryCreatedBy = Ersteller;
            }
            eintrag.LastChange = jetztOrtszeit;
            eintrag.LastChangedBy = Ersteller;
        }

        /// <summary>
        /// Bei offenem Ende eine halbe Stunde: der Kalender zeigt dann nur den Titel und keine erfundene Endzeit
        /// (Kalender.razor blendet die Uhrzeit erst ab mehr als 30 Minuten ein).
        /// </summary>
        private static DateTime Ende(VeranstaltungsTag tag) =>
            tag.Ende is { } ende ? tag.Datum.ToDateTime(ende) : tag.Datum.ToDateTime(tag.Beginn).AddMinutes(30);
    }
}
