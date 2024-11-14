using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace BudoShurenWebsite.Services
{
    public class VisitorCounterService(IDbContextFactory<ApplicationDbContext> dbContextFactory, IHttpContextAccessor httpContextAccessor, ILogger<VisitorCounterService> logger)
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory = dbContextFactory;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly ILogger<VisitorCounterService> _logger = logger;
        private const string VisitorCookieName = "VisitorId";
        private int _visitorCount = 0;
        private readonly List<string> _knownBots = new List<string>
    {
        "Googlebot",
        "Bingbot",
        "Slurp",
        "DuckDuckBot",
        "Baiduspider",
        "YandexBot",
        "Sogou",
        "Exabot",
        "facebot",
        "ia_archiver"
    };

        private readonly Regex _botRegex = new Regex(@"bot|crawler|slurp|spider|mediapartners|baiduspider|80legs|ia_archiver|voyager|curl|wget", RegexOptions.IgnoreCase);
        //private readonly Regex _botRegex = new Regex(@"bot|crawl|slurp|spider|mediapartners", RegexOptions.IgnoreCase);


        public int VisitorCount => _visitorCount;

        public async Task AddVisitorAsync(string? visitorId, string pageName)
        {
            if (CheckForBot())
            {
                _logger.LogDebug("Bot detected, not counting visit.");
                return;
            }

            if (string.IsNullOrEmpty(visitorId))
            {
                visitorId = Guid.NewGuid().ToString();
                // Set the cookie in the response
                var context = _httpContextAccessor.HttpContext;
                context?.Response.Cookies.Append(VisitorCookieName, visitorId, new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddYears(1),
                    HttpOnly = true,
                    Secure = true
                });
            }

            try
            {
                // Normalize the pageName
                if (!string.IsNullOrEmpty(pageName) && pageName.StartsWith("budoshuren/"))
                {
                    pageName = pageName.Substring("budoshuren/".Length);
                }

                if (!string.IsNullOrEmpty(pageName) && pageName.StartsWith("Account/"))
                {
                    pageName = pageName.Substring("Account/".Length);
                }

                if (!string.IsNullOrEmpty(pageName) && pageName.StartsWith("Login?ReturnUrl"))
                {
                    pageName = "Login";
                }   
                if (!string.IsNullOrEmpty(pageName) && pageName.StartsWith("Auth?ReturnUrl"))
                {
                    pageName = "Login";
                }

                if (!string.IsNullOrEmpty(pageName) && pageName.StartsWith("ResetPassword?"))
                {
                    pageName = "ResetPassword";
                }     
                
                if (!string.IsNullOrEmpty(pageName) && pageName.Contains("?"))
                {
                    pageName = pageName.Substring(0, pageName.IndexOf("?"));
                }

                using var dbContext = _dbContextFactory.CreateDbContext();
                var visit = new Visit
                {
                    Timestamp = DateTime.UtcNow,
                    VisitorID = visitorId,
                    PageName = string.IsNullOrEmpty(pageName) ? "Home" : pageName
                };
                dbContext.Visits.Add(visit);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add visit to database");
                // Log or handle the exception as needed
            }
        }

        public async Task<int> GetNumberOfCallsLast30Days()
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var now = DateTime.UtcNow;
                var startOfLast30Days = now.AddDays(-30);
                return await context.Visits.CountAsync(v => v.Timestamp >= startOfLast30Days);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add visit to database");

                // Log or handle the exception as needed
                return -1;
            }

        }
        public async Task<int> GetUniqueVisitorsLast30Days()
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var now = DateTime.UtcNow;
                var startOfLast30Days = now.AddDays(-30);
                return await context.Visits
                    .Where(v => v.Timestamp >= startOfLast30Days)
                    .Select(v => v.VisitorID)
                    .Distinct()
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate unique visits for the last 30 days");
                return -1;
            }
        }

        public async Task<Dictionary<string, (int UniqueVisitors, int TotalVisits)>> GetUniqueVisitorsPerPageLast30Days()
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var now = DateTime.UtcNow;
                var startOfLast30Days = now.AddDays(-30);

                var result = await context.Visits
                    .Where(v => v.Timestamp >= startOfLast30Days)
                    .GroupBy(v => v.PageName)
                    .Select(g => new
                    {
                        PageName = g.Key,
                        UniqueVisitors = g.Select(v => v.VisitorID).Distinct().Count(),
                        TotalVisits = g.Count()
                    })
                    .ToDictionaryAsync(x => x.PageName, x => (x.UniqueVisitors, x.TotalVisits));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate unique visitors and total visits per page for the last 30 days");
                return new Dictionary<string, (int UniqueVisitors, int TotalVisits)>();
            }
        }

        private bool CheckForBot()
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null)
            {
                return false;
            }

            var userAgent = context.Request.Headers["User-Agent"].ToString();
            if (string.IsNullOrEmpty(userAgent))
            {
                return false;
            }

            // Check against known bots list
            foreach (var bot in _knownBots)
            {
                if (userAgent.Contains(bot, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Check against bot regex
            if (_botRegex.IsMatch(userAgent))
            {
                return true;
            }

            return false;
        }


        //public async Task<Dictionary<string, int>> GetUniqueVisitorsPerPageLast30Days()
        //{
        //    try
        //    {
        //        using var context = _dbContextFactory.CreateDbContext();
        //        var now = DateTime.UtcNow;
        //        var startOfLast30Days = now.AddDays(-30);

        //        var result = await context.Visits
        //            .Where(v => v.Timestamp >= startOfLast30Days)
        //            .GroupBy(v => v.PageName)
        //            .Select(g => new
        //            {
        //                PageName = g.Key,
        //                UniqueVisitors = g.Select(v => v.VisitorID).Distinct().Count()
        //            })
        //            .ToDictionaryAsync(x => x.PageName, x => x.UniqueVisitors);

        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log or handle the exception as needed
        //        return new Dictionary<string, int>();
        //    }
        //}
    }
}