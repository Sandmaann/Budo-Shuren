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

        /// <summary>Ergänzt bei Einträgen von Veranstaltungen Id und Slug der Veranstaltung (für Links im Kalender).</summary>
        public static async Task VeranstaltungenZuordnenAsync(ApplicationDbContext kontext, IReadOnlyCollection<AppointmentData> eintraege, CancellationToken abbruch = default)
        {
            var tagIds = eintraege.Where(e => e.VeranstaltungsTagId != null).Select(e => e.VeranstaltungsTagId!.Value).Distinct().ToList();
            if (tagIds.Count == 0)
                return;

            var veranstaltungen = await kontext.VeranstaltungsTage.AsNoTracking()
                .Where(t => tagIds.Contains(t.Id))
                .Select(t => new { t.Id, t.VeranstaltungId, t.Veranstaltung!.Slug })
                .ToDictionaryAsync(t => t.Id, abbruch);

            foreach (var eintrag in eintraege)
            {
                if (eintrag.VeranstaltungsTagId is { } tagId && veranstaltungen.TryGetValue(tagId, out var v))
                {
                    eintrag.VeranstaltungId = v.VeranstaltungId;
                    eintrag.VeranstaltungSlug = v.Slug;
                }
            }
        }
    }
}
