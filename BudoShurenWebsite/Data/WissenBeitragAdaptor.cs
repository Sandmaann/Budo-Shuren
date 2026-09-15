using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;

namespace BudoShurenWebsite.Data
{
    public class WissenBeitragAdaptor : DataAdaptor
    {
        private readonly UserService _userService;
        private readonly ApplicationDbContext _dbContext;
        private readonly WissenService _wissenService;
        private readonly ILogger<WissenBeitragAdaptor> _logger;

        public WissenBeitragAdaptor(
            UserService userService,
            ApplicationDbContext dbContext,
            WissenService wissenService,
            ILogger<WissenBeitragAdaptor> logger)
        {
            _userService = userService;
            _dbContext = dbContext;
            _wissenService = wissenService;
            _logger = logger;
        }

        public override async Task<object> ReadAsync(DataManagerRequest dm, string? Key = null)
        {
            try
            {
                IEnumerable<WissenBeitrag> dataSource = await _dbContext.WissenBeitraege
                    .Include(b => b.Kategorie)
                    .ToListAsync();

                int totalRecordsCount = dataSource.Count();

                if (dm.Search != null && dm.Search.Count > 0)
                {
                    dataSource = DataOperations.PerformSearching(dataSource, dm.Search);
                }

                if (dm.Sorted != null && dm.Sorted.Count > 0)
                {
                    dataSource = DataOperations.PerformSorting(dataSource, dm.Sorted);
                }

                int count = dataSource.Count();

                if (dm.Skip != 0)
                {
                    dataSource = DataOperations.PerformSkip(dataSource, dm.Skip);
                }

                if (dm.Take != 0)
                {
                    dataSource = DataOperations.PerformTake(dataSource, dm.Take);
                }

                return dm.RequiresCounts ? new DataResult() { Result = dataSource, Count = count } : (object)dataSource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Laden der Wissensbeiträge");
                throw;
            }
        }

        public override async Task<object> InsertAsync(DataManager dataManager, object record, string additionalParam)
        {
            try
            {
                if (record is not WissenBeitrag obj)
                {
                    throw new Exception("Ungültiger Datensatz");
                }

                await _userService.InitializeAsync();
                if (!(_userService.IsEditor || _userService.IsAbteilungsleiter || _userService.IsAdmin))
                {
                    throw new Exception("Keine Berechtigung zum Erstellen von Beiträgen");
                }

                obj.Erstellt = DateTime.Now;
                obj.ErstelltVon = _userService.CurrentUser?.UserName ?? "unbekannt";
                obj.Geaendert = DateTime.Now;
                obj.GeaendertVon = _userService.CurrentUser?.UserName ?? "unbekannt";

                await _wissenService.CreateBeitragAsync(obj);

                return obj;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Erstellen des Wissensbeitrags");
                throw;
            }
        }

        public override async Task<object> UpdateAsync(DataManager dm, object value, string keyField, string key)
        {
            try
            {
                if (value is not WissenBeitrag obj)
                {
                    throw new Exception("Ungültiger Datensatz");
                }

                await _userService.InitializeAsync();
                if (!(_userService.IsEditor || _userService.IsAbteilungsleiter || _userService.IsAdmin))
                {
                    throw new Exception("Keine Berechtigung zum Bearbeiten von Beiträgen");
                }

                var existingBeitrag = await _dbContext.WissenBeitraege.FindAsync(obj.Id);
                if (existingBeitrag == null)
                {
                    throw new Exception($"Beitrag mit ID {obj.Id} nicht gefunden");
                }

                existingBeitrag.Titel = obj.Titel;
                existingBeitrag.Slug = obj.Slug;
                existingBeitrag.KategorieId = obj.KategorieId;
                existingBeitrag.MetaTitel = obj.MetaTitel;
                existingBeitrag.MetaBeschreibung = obj.MetaBeschreibung;
                existingBeitrag.Veroeffentlicht = obj.Veroeffentlicht;
                existingBeitrag.SortOrder = obj.SortOrder;
                existingBeitrag.Geaendert = DateTime.Now;
                existingBeitrag.GeaendertVon = _userService.CurrentUser?.UserName ?? "unbekannt";

                await _wissenService.UpdateBeitragAsync(existingBeitrag);

                return existingBeitrag;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Aktualisieren des Wissensbeitrags");
                throw;
            }
        }

        public override async Task<object> RemoveAsync(DataManager dm, object value, string keyField, string key)
        {
            try
            {
                await _userService.InitializeAsync();
                if (!(_userService.IsEditor || _userService.IsAbteilungsleiter || _userService.IsAdmin))
                {
                    throw new Exception("Keine Berechtigung zum Löschen von Beiträgen");
                }

                int id;
                if (value is WissenBeitrag beitrag)
                {
                    id = beitrag.Id;
                }
                else if (int.TryParse(key, out int parsedId))
                {
                    id = parsedId;
                }
                else
                {
                    throw new Exception("Ungültige ID");
                }

                await _wissenService.DeleteBeitragAsync(id);

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Löschen des Wissensbeitrags");
                throw;
            }
        }
    }
}
