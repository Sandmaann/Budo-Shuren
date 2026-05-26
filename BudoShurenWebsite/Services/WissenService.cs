using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BudoShurenWebsite.Services
{
    public class WissenService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ImageService _imageService;
        private readonly ILogger<WissenService> _logger;

        private const string CacheKeyAll = "wissen_alle";
        private const string CacheKeyPrefix = "wissen_";
        private readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public WissenService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ImageService imageService,
            ILogger<WissenService> logger)
        {
            _context = context;
            _cache = cache;
            _imageService = imageService;
            _logger = logger;
        }

        #region Kategorien

        public async Task<List<WissenKategorie>> GetKategorienAsync()
        {
            return await _context.WissenKategorien
                .Include(k => k.Abteilung)
                .OrderBy(k => k.SortOrder)
                .ToListAsync();
        }

        public async Task<WissenKategorie?> GetKategorieByIdAsync(int id)
        {
            return await _context.WissenKategorien
                .Include(k => k.Abteilung)
                .FirstOrDefaultAsync(k => k.Id == id);
        }

        public async Task<WissenKategorie> CreateKategorieAsync(WissenKategorie kategorie)
        {
            _context.WissenKategorien.Add(kategorie);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return kategorie;
        }

        public async Task<WissenKategorie> UpdateKategorieAsync(WissenKategorie kategorie)
        {
            _context.WissenKategorien.Update(kategorie);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return kategorie;
        }

        public async Task DeleteKategorieAsync(int id)
        {
            var kategorie = await _context.WissenKategorien.FindAsync(id);
            if (kategorie != null)
            {
                _context.WissenKategorien.Remove(kategorie);
                await _context.SaveChangesAsync();
                InvalidateCache();
            }
        }

        #endregion

        #region Beiträge

        public async Task<List<WissenBeitrag>> GetBeitraegeAsync(bool nurVeroeffentlichte = true)
        {
            var cacheKey = nurVeroeffentlichte ? $"{CacheKeyAll}_published" : CacheKeyAll;

            if (_cache.TryGetValue(cacheKey, out List<WissenBeitrag>? cachedBeitraege) && cachedBeitraege != null)
            {
                return cachedBeitraege;
            }

            var query = _context.WissenBeitraege
                .Include(b => b.Kategorie)
                .Include(b => b.Bloecke)
                .OrderBy(b => b.SortOrder)
                .AsQueryable();

            if (nurVeroeffentlichte)
            {
                query = query.Where(b => b.Veroeffentlicht);
            }

            var beitraege = await query.ToListAsync();

            _cache.Set(cacheKey, beitraege, CacheDuration);

            return beitraege;
        }

        public async Task<WissenBeitrag?> GetBeitragBySlugAsync(string slug, bool nurVeroeffentlichte = true)
        {
            var cacheKey = $"{CacheKeyPrefix}{slug}";

            if (_cache.TryGetValue(cacheKey, out WissenBeitrag? cachedBeitrag) && cachedBeitrag != null)
            {
                if (!nurVeroeffentlichte || cachedBeitrag.Veroeffentlicht)
                {
                    return cachedBeitrag;
                }
            }

            var query = _context.WissenBeitraege
                .Include(b => b.Kategorie)
                .Include(b => b.Bloecke)
                .AsQueryable();

            if (nurVeroeffentlichte)
            {
                query = query.Where(b => b.Veroeffentlicht);
            }

            var beitrag = await query.FirstOrDefaultAsync(b => b.Slug == slug);

            if (beitrag != null)
            {
                _cache.Set(cacheKey, beitrag, CacheDuration);
            }

            return beitrag;
        }

        public async Task<WissenBeitrag?> GetBeitragByIdAsync(int id)
        {
            return await _context.WissenBeitraege
                .Include(b => b.Kategorie)
                .Include(b => b.Bloecke.OrderBy(bl => bl.Sortierung))
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<WissenBeitrag> CreateBeitragAsync(WissenBeitrag beitrag)
        {
            _context.WissenBeitraege.Add(beitrag);
            await _context.SaveChangesAsync();
            InvalidateCache();
            return beitrag;
        }

        public async Task<WissenBeitrag> UpdateBeitragAsync(WissenBeitrag beitrag)
        {
            _context.WissenBeitraege.Update(beitrag);
            await _context.SaveChangesAsync();
            InvalidateCache(beitrag.Slug);
            return beitrag;
        }

        public async Task DeleteBeitragAsync(int id)
        {
            var beitrag = await _context.WissenBeitraege
                .Include(b => b.Bloecke)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (beitrag != null)
            {
                // Löschen aller zugehörigen Bilder
                foreach (var block in beitrag.Bloecke.Where(bl => bl.BildId.HasValue))
                {
                    try
                    {
                        await _imageService.RemoveImageAsync(block.BildId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Fehler beim Löschen des Bildes {block.BildId} für Block {block.Id}");
                    }
                }

                _context.WissenBeitraege.Remove(beitrag);
                await _context.SaveChangesAsync();
                InvalidateCache(beitrag.Slug);
            }
        }

        #endregion

        #region Blöcke

        public async Task<WissenBlock> CreateBlockAsync(WissenBlock block)
        {
            _context.WissenBloecke.Add(block);
            await _context.SaveChangesAsync();
            await InvalidateCacheForBeitragAsync(block.BeitragId);
            return block;
        }

        public async Task<WissenBlock> UpdateBlockAsync(WissenBlock block)
        {
            _context.WissenBloecke.Update(block);
            await _context.SaveChangesAsync();
            await InvalidateCacheForBeitragAsync(block.BeitragId);
            return block;
        }

        public async Task DeleteBlockAsync(int id)
        {
            var block = await _context.WissenBloecke.FindAsync(id);
            if (block != null)
            {
                // Löschen des zugehörigen Bildes
                if (block.BildId.HasValue)
                {
                    try
                    {
                        await _imageService.RemoveImageAsync(block.BildId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Fehler beim Löschen des Bildes {block.BildId} für Block {id}");
                    }
                }

                _context.WissenBloecke.Remove(block);
                await _context.SaveChangesAsync();
                await InvalidateCacheForBeitragAsync(block.BeitragId);
            }
        }

        public async Task SwapBlockSortierungAsync(int blockId, bool moveUp)
        {
            var block = await _context.WissenBloecke.FindAsync(blockId);
            if (block == null) return;

            var bloecke = await _context.WissenBloecke
                .Where(b => b.BeitragId == block.BeitragId)
                .OrderBy(b => b.Sortierung)
                .ToListAsync();

            var index = bloecke.IndexOf(block);
            if (index == -1) return;

            var targetIndex = moveUp ? index - 1 : index + 1;
            if (targetIndex < 0 || targetIndex >= bloecke.Count) return;

            var targetBlock = bloecke[targetIndex];

            // Swap Sortierung
            (block.Sortierung, targetBlock.Sortierung) = (targetBlock.Sortierung, block.Sortierung);

            _context.WissenBloecke.UpdateRange(block, targetBlock);
            await _context.SaveChangesAsync();
            await InvalidateCacheForBeitragAsync(block.BeitragId);
        }

        #endregion

        #region Cache-Management

        private void InvalidateCache(string? slug = null)
        {
            _cache.Remove(CacheKeyAll);
            _cache.Remove($"{CacheKeyAll}_published");

            if (!string.IsNullOrEmpty(slug))
            {
                _cache.Remove($"{CacheKeyPrefix}{slug}");
            }
        }

        private async Task InvalidateCacheForBeitragAsync(int beitragId)
        {
            var beitrag = await _context.WissenBeitraege.FindAsync(beitragId);
            if (beitrag != null)
            {
                InvalidateCache(beitrag.Slug);
            }
        }

        #endregion
    }
}
