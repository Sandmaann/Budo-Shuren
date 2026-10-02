using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Blazor;
using Syncfusion.Blazor.Data;

namespace BudoShurenWebsite.Data
{
    public class AktuellesBeitragAdaptor : DataAdaptor
    {
        private readonly UserService _userService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IAktuellesService _aktuellesService;
        private readonly ILogger<AktuellesBeitragAdaptor> _logger;

        public AktuellesBeitragAdaptor(
            UserService userService,
            ApplicationDbContext dbContext,
            IAktuellesService aktuellesService,
            ILogger<AktuellesBeitragAdaptor> logger)
        {
            _userService = userService;
            _dbContext = dbContext;
            _aktuellesService = aktuellesService;
            _logger = logger;
        }

        public override async Task<object> ReadAsync(DataManagerRequest dm, string? Key = null)
        {
            try
            {
                IEnumerable<AktuellesBeitrag> dataSource = await _dbContext.AktuellesBeitraege
                    .Include(b => b.Abteilung)
                    .OrderByDescending(b => b.Datum)
                    .ToListAsync();

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
                _logger.LogError(ex, "Fehler beim Laden der Aktuelles-Beiträge");
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
                if (value is AktuellesBeitrag beitrag)
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

                // Über den Service, damit auch die Bilddaten des Beitrags gelöscht werden
                await _aktuellesService.Loeschen(id);

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Löschen des Aktuelles-Beitrags");
                throw;
            }
        }
    }
}
