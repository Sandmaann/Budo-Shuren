using BudoShurenWebsite.Data;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services
{
    /// <summary>
    /// Wo die Website Bilder (DbImage) verwendet, und das Übernehmen vorläufiger Uploads (DbImage.VorlaeufigSeitUtc).
    /// Die Editoren von Aktuelles und Veranstaltungen laden Bilder sofort hoch, verwendet werden sie erst mit dem Speichern.
    /// </summary>
    public static class BildVerwendung
    {
        /// <summary>
        /// Alle Bilder, auf die Inhalte der Website verweisen (ohne die Galerien der angegebenen Veranstaltung).
        /// Muss jeden Fremdschlüssel auf Images enthalten: BildAufraeumJob löscht nur, was hier nicht vorkommt.
        /// </summary>
        public static IQueryable<int> VerwendeteBildIds(ApplicationDbContext kontext, int? ohneVeranstaltungId = null) =>
            kontext.Galerie.Where(g => g.DbImageId != null).Select(g => g.DbImageId!.Value)
                .Concat(kontext.Neuigkeiten.Where(n => n.DbImageId != null).Select(n => n.DbImageId!.Value))
                .Concat(kontext.WissenBloecke.Where(w => w.BildId != null).Select(w => w.BildId!.Value))
                .Concat(kontext.AktuellesBilder.Select(a => a.BildId))
                .Concat(kontext.VeranstaltungBilder.Where(b => b.Block!.VeranstaltungId != ohneVeranstaltungId).Select(b => b.BildId));

        /// <summary>
        /// Markiert die Bilder als gespeichert, damit BildAufraeumJob sie nicht mehr löscht.
        /// Wirkt sofort in der Datenbank (nicht erst mit SaveChanges): deshalb in derselben Transaktion aufrufen wie das Speichern des Inhalts.
        /// </summary>
        public static async Task AlsGespeichertMarkierenAsync(ApplicationDbContext kontext, IReadOnlyCollection<int> bildIds, CancellationToken abbruch = default)
        {
            if (bildIds.Count == 0)
                return;
            await kontext.Images
                .Where(i => bildIds.Contains(i.Id) && i.VorlaeufigSeitUtc != null)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.VorlaeufigSeitUtc, (DateTime?)null), abbruch);
        }
    }
}
