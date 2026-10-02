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
            // Eine Transaktion: die neu hochgeladenen Bilder gelten genau dann als gespeichert, wenn der Beitrag gespeichert ist
            await using var transaktion = await _context.Database.BeginTransactionAsync();

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
            await BildVerwendung.AlsGespeichertMarkierenAsync(_context, beitrag.Bloecke.SelectMany(b => b.Bilder).Select(b => b.BildId).Distinct().ToList());
            await transaktion.CommitAsync();
        }

        public async Task Loeschen(int id)
        {
            var beitrag = await _context.AktuellesBeitraege.FindAsync(id);
            if (beitrag != null)
            {
                _context.AktuellesBeitraege.Remove(beitrag);
                await _context.SaveChangesAsync();
            }
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
