using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Veranstaltungen;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Bringt die Kalendereinträge einer Veranstaltung auf den aktuellen Stand (anlegen, aktualisieren, entfernen).
    /// Gespeichert wird mit dem SaveChanges des Aufrufers; neue Tage brauchen dafür schon ihre Id.
    /// </summary>
    public static class KalenderAbgleich
    {
        /// <param name="veranstaltung">Mit geladenen Tagen und Abteilung.</param>
        public static async Task AbgleichenAsync(ApplicationDbContext kontext, Veranstaltung veranstaltung, DateTime jetztOrtszeit, CancellationToken abbruch)
        {
            var tagIds = veranstaltung.Tage.Select(t => t.Id).ToList();
            var eintraege = await kontext.Appointments
                .Where(a => a.VeranstaltungsTagId != null && tagIds.Contains(a.VeranstaltungsTagId.Value))
                .ToListAsync(abbruch);

            foreach (var tag in veranstaltung.Tage)
            {
                var eintrag = eintraege.SingleOrDefault(e => e.VeranstaltungsTagId == tag.Id);
                if (KalenderEintragFabrik.GehoertInDenKalender(veranstaltung, tag))
                {
                    if (eintrag is null)
                    {
                        eintrag = new AppointmentData();
                        kontext.Appointments.Add(eintrag);
                    }
                    KalenderEintragFabrik.Uebernehmen(eintrag, veranstaltung, tag, jetztOrtszeit);
                }
                else if (eintrag is not null)
                {
                    kontext.Appointments.Remove(eintrag);
                }
            }
        }
    }
}
