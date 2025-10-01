using BudoShurenWebsite.Global;
using BudoShurenWebsite.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.Internal;

namespace BudoShurenWebsite.Controllers
{
    [AllowAnonymous]
    [Route("Custom/[controller]")]
    [ApiController]
    public class ApiController : Controller
    {

        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly ILogger<ApiController> logger;
        private readonly VisitorCounterService visitorCounterService;

        public ApiController(
            IHostApplicationLifetime applicationLifetime,
            ILogger<ApiController> logger,
            VisitorCounterService visitorCounterService
            )
        {
            this.applicationLifetime = applicationLifetime;
            this.logger = logger;
            this.visitorCounterService = visitorCounterService;
        }

        [HttpGet("StopApp")]
        public async Task<IActionResult> StopApp()
        {
            logger.Log(LogLevel.Information, "Stoppe API (Admin)");
            try
            {
                await visitorCounterService.AddVisitorAsync("API", "Stop");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add visit to database (Stopp)");
            }
            applicationLifetime.StopApplication();
            return new EmptyResult();
        }
    }
}
