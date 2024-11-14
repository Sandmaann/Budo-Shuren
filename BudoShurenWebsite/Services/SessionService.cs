namespace BudoShurenWebsite.Services
{
    public class SessionService(
        IHttpContextAccessor httpContextAccessor,
        ILogger<SessionService> logger)
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly ILogger<SessionService> _logger = logger;

        public void SetUsername(string username)
        {
            _logger.LogDebug("Setting username in session: {Username}", username);
            var httpContext = _httpContextAccessor.HttpContext;
            var session = httpContext?.Session;
            session?.SetString("Username", username);
        }

        public string? GetUsername()
        {
            _logger.LogDebug("Getting username from session");
            var httpContext = _httpContextAccessor.HttpContext;
            var session = httpContext?.Session;
            var result = session?.GetString("Username");
            return result;
        }
    }
}
