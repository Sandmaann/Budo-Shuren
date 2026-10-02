using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services
{
    public class AktuellesService : IAktuellesService
    {
        private readonly ApplicationDbContext _context;

        public AktuellesService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(List<AktuellesBeitrag> items, int total)> GetBeitraege(int skip, int take, string? suchtext, string? abteilungId)
        {
            var query = _context.AktuellesBeitraege
                .Include(b => b.Bloecke.OrderBy(bl => bl.Sortierung))
                    .ThenInclude(bl => bl.Bilder.OrderBy(bi => bi.Sortierung))
                        .ThenInclude(bi => bi.Bild)
                .Include(b => b.Abteilung)
                .Where(b => b.Veroeffentlicht);

            // Suchtext-Filter auf Titel
            if (!string.IsNullOrWhiteSpace(suchtext))
            {
                query = query.Where(b => b.Titel.Contains(suchtext));
            }

            // Abteilungs-Filter
            if (!string.IsNullOrWhiteSpace(abteilungId))
            {
                query = query.Where(b => b.AbteilungId == abteilungId);
            }

            // Sortierung: absteigend nach Datum (neueste zuerst)
            query = query.OrderByDescending(b => b.Datum);

            var total = await query.CountAsync();

            var items = await query
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return (items, total);
        }

        public async Task<AktuellesBeitrag?> GetBeitragBySlug(string slug)
        {
            return await _context.AktuellesBeitraege
                .Include(b => b.Bloecke.OrderBy(bl => bl.Sortierung))
                    .ThenInclude(bl => bl.Bilder.OrderBy(bi => bi.Sortierung))
                        .ThenInclude(bi => bi.Bild)
                .Include(b => b.Abteilung)
                .Where(b => b.Veroeffentlicht)
                .FirstOrDefaultAsync(b => b.Slug == slug);
        }

        public async Task<AktuellesBeitrag?> GetById(int id)
        {
            return await _context.AktuellesBeitraege
                .Include(b => b.Bloecke.OrderBy(bl => bl.Sortierung))
                    .ThenInclude(bl => bl.Bilder.OrderBy(bi => bi.Sortierung))
                        .ThenInclude(bi => bi.Bild)
                .Include(b => b.Abteilung)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task Speichern(AktuellesBeitrag beitrag)
        {
            // Eine Transaktion: Bilder gelten genau dann als gespeichert bzw. werden genau dann gelöscht, wenn der Beitrag gespeichert ist
            await using var transaktion = await _context.Database.BeginTransactionAsync();

            // Stand in der Datenbank, nicht im bearbeiteten Objekt: daraus ergibt sich, welche Bilder entfernt wurden
            var bisherigeBildIds = beitrag.Id == 0 ? [] : await BildIdsAsync(beitrag.Id);

            if (beitrag.Id == 0)
            {
                beitrag.Erstellt = DateTime.Now;
                _context.AktuellesBeitraege.Add(beitrag);
            }
            else
            {
                beitrag.Geaendert = DateTime.Now;
                _context.AktuellesBeitraege.Update(beitrag);
            }

            await _context.SaveChangesAsync();
            var bildIds = beitrag.Bloecke.SelectMany(b => b.Bilder).Select(b => b.BildId).Distinct().ToList();
            await BildVerwendung.AlsGespeichertMarkierenAsync(_context, bildIds);
            await BilderLoeschenAsync(bisherigeBildIds.Except(bildIds).ToList());
            await transaktion.CommitAsync();
        }

        public async Task Loeschen(int id)
        {
            var beitrag = await _context.AktuellesBeitraege.FindAsync(id);
            if (beitrag != null)
            {
                await using var transaktion = await _context.Database.BeginTransactionAsync();
                var bildIds = await BildIdsAsync(id);
                // Blöcke und ihre Bildzuordnungen löscht die Datenbank mit (Cascade), die Bilddaten danach BilderLoeschenAsync
                _context.AktuellesBeitraege.Remove(beitrag);
                await _context.SaveChangesAsync();
                await BilderLoeschenAsync(bildIds);
                await transaktion.CommitAsync();
            }
        }

        private Task<List<int>> BildIdsAsync(int beitragId) =>
            _context.AktuellesBilder.Where(b => b.Block!.BeitragId == beitragId).Select(b => b.BildId).Distinct().ToListAsync();

        /// <summary>Löscht die Bilddaten entfernter Bilder, sofern sie nirgends mehr verwendet werden.</summary>
        private async Task BilderLoeschenAsync(IReadOnlyCollection<int> bildIds)
        {
            await BildVerwendung.UnverwendeteLoeschenAsync(_context, bildIds);
            // Der Kontext lebt so lange wie die Seite: gelöschte Bilder nicht weiter verfolgen
            foreach (var eintrag in _context.ChangeTracker.Entries<DbImage>().Where(e => bildIds.Contains(e.Entity.Id)).ToList())
                eintrag.State = EntityState.Detached;
        }

        public async Task<bool> SlugExists(string slug, int? excludeId = null)
        {
            var query = _context.AktuellesBeitraege.Where(b => b.Slug == slug);

            if (excludeId.HasValue)
            {
                query = query.Where(b => b.Id != excludeId.Value);
            }

            return await query.AnyAsync();
        }
    }
}
