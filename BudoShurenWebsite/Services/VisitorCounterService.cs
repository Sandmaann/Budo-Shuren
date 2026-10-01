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
        public int VisitorCount => _visitorCount;

        public async Task AddVisitorAsync(string? visitorId, string pageName)
        {
            try
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
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to add visit to database 1");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add visit to database 2");
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

        // Helper method to get visitor metrics for a specific time period
        private async Task<VisitorMetrics> GetVisitorMetricsAsync(DateTime startDate)
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var now = DateTime.UtcNow;

                var visitsInPeriod = await context.Visits
                    .Where(v => v.Timestamp >= startDate && v.Timestamp <= now)
                    .ToListAsync();

                if (!visitsInPeriod.Any())
                {
                    return new VisitorMetrics();
                }

                var uniqueVisitors = visitsInPeriod.Select(v => v.VisitorID).Distinct().Count();
                var totalVisits = visitsInPeriod.Count;

                // Gruppiere nach VisitorID
                var visitorGroups = visitsInPeriod.GroupBy(v => v.VisitorID).ToList();

                // Berechne Visits pro Visitor
                var visitsPerVisitor = visitorGroups.Select(g => g.Count()).OrderBy(x => x).ToList();

                var repeatVisitors = visitorGroups.Count(g => g.Count() > 1);
                var avgPagesPerVisitor = visitorGroups.Any() ? Math.Round((double)totalVisits / uniqueVisitors, 2) : 0;

                // Median berechnen
                double medianPagesPerVisitor = 0;
                if (visitsPerVisitor.Any())
                {
                    int count = visitsPerVisitor.Count;
                    if (count % 2 == 0)
                    {
                        // Gerade Anzahl: Durchschnitt der zwei mittleren Werte
                        medianPagesPerVisitor = Math.Round((visitsPerVisitor[count / 2 - 1] + visitsPerVisitor[count / 2]) / 2.0, 2);
                    }
                    else
                    {
                        // Ungerade Anzahl: der mittlere Wert
                        medianPagesPerVisitor = visitsPerVisitor[count / 2];
                    }
                }

                // Bounce Rate (nur 1 Seite besucht)
                var bounceVisitors = visitorGroups.Count(g => g.Count() == 1);
                var bounceRate = uniqueVisitors > 0 ? Math.Round((double)bounceVisitors / uniqueVisitors * 100, 2) : 0;

                return new VisitorMetrics
                {
                    TotalVisits = totalVisits,
                    UniqueVisitors = uniqueVisitors,
                    RepeatVisitors = repeatVisitors,
                    AvgPagesPerVisitor = avgPagesPerVisitor,
                    MedianPagesPerVisitor = medianPagesPerVisitor,
                    BounceRate = bounceRate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate visitor metrics");
                return new VisitorMetrics { TotalVisits = -1, UniqueVisitors = -1 };
            }
        }

        public async Task<VisitorMetrics> GetMetricsForTodayAsync()
        {
            var todayStart = DateTime.UtcNow.Date;
            return await GetVisitorMetricsAsync(todayStart);
        }

        public async Task<VisitorMetrics> GetMetricsForLast7DaysAsync()
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            return await GetVisitorMetricsAsync(sevenDaysAgo);
        }

        public async Task<VisitorMetrics> GetMetricsForLast30DaysAsync()
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            return await GetVisitorMetricsAsync(thirtyDaysAgo);
        }

        public async Task<Dictionary<string, VisitorMetricsPerPage>> GetPageMetricsForTodayAsync()
        {
            return await GetPageMetricsAsync(DateTime.UtcNow.Date);
        }

        public async Task<Dictionary<string, VisitorMetricsPerPage>> GetPageMetricsForLast7DaysAsync()
        {
            return await GetPageMetricsAsync(DateTime.UtcNow.AddDays(-7));
        }

        public async Task<Dictionary<string, VisitorMetricsPerPage>> GetPageMetricsForLast30DaysAsync()
        {
            return await GetPageMetricsAsync(DateTime.UtcNow.AddDays(-30));
        }

        private async Task<Dictionary<string, VisitorMetricsPerPage>> GetPageMetricsAsync(DateTime startDate)
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var now = DateTime.UtcNow;

                var visitsInPeriod = await context.Visits
                    .Where(v => v.Timestamp >= startDate && v.Timestamp <= now)
                    .ToListAsync();

                if (!visitsInPeriod.Any())
                {
                    return new Dictionary<string, VisitorMetricsPerPage>();
                }

                var result = visitsInPeriod
                    .GroupBy(v => v.PageName)
                    .ToDictionary(
                        g => g.Key,
                        g => new VisitorMetricsPerPage
                        {
                            UniqueVisitors = g.Select(v => v.VisitorID).Distinct().Count(),
                            TotalVisits = g.Count()
                        }
                    );

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate page metrics");
                return new Dictionary<string, VisitorMetricsPerPage>();
            }
        }

        // Legacy methods for backward compatibility
        public async Task<int> GetNumberOfCallsLast30Days()
        {
            var metrics = await GetMetricsForLast30DaysAsync();
            return metrics.TotalVisits;
        }

        public async Task<int> GetUniqueVisitorsLast30Days()
        {
            var metrics = await GetMetricsForLast30DaysAsync();
            return metrics.UniqueVisitors;
        }

        public async Task<Dictionary<string, (int UniqueVisitors, int TotalVisits)>> GetUniqueVisitorsPerPageLast30Days()
        {
            var metrics = await GetPageMetricsForLast30DaysAsync();
            return metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.UniqueVisitors, kvp.Value.TotalVisits)
            );
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

            if (_knownBots.Any(bot => userAgent.Contains(bot, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return _botRegex.IsMatch(userAgent);
        }
    }

    public class VisitorMetrics
    {
        public int TotalVisits { get; set; } = 0;
        public int UniqueVisitors { get; set; } = 0;
        public int RepeatVisitors { get; set; } = 0;
        public double AvgPagesPerVisitor { get; set; } = 0;
        public double MedianPagesPerVisitor { get; set; } = 0;
        public double BounceRate { get; set; } = 0;
    }

    public class VisitorMetricsPerPage
    {
        public int UniqueVisitors { get; set; } = 0;
        public int TotalVisits { get; set; } = 0;
    }
}