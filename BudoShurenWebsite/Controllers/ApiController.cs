using BudoShurenWebsite.Global;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting.Internal;

namespace BudoShurenWebsite.Controllers
{
    //[Authorize]
    //[Authorize(Policy = "NotGuest")]
    //[Authorize(Policy = "Aktiviert")]
    //[Authorize(Roles = Roles.Admin)]

    //https://localhost:7280/Custom/Api/StopApp

    [AllowAnonymous]
    [Route("Custom/[controller]")]
    [ApiController]
    public class ApiController : Controller
    {

        private readonly IHostApplicationLifetime applicationLifetime;
        private readonly ILogger<ApiController> logger;

        public ApiController(IHostApplicationLifetime applicationLifetime,
            ILogger<ApiController> logger)
        {
            this.applicationLifetime = applicationLifetime;
            this.logger = logger;
        }

        [HttpGet("StopApp")]
        public IActionResult StopApp()
        {
            logger.Log(LogLevel.Information, "Stoppe API (Admin)");
            applicationLifetime.StopApplication();
            return new EmptyResult();
        }
    }
}
