using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace BudoShurenWebsite.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IHostEnvironment env;
        private readonly ILogger<FilesaveController> logger;

        public DocumentsController(IHostEnvironment env,
            ILogger<FilesaveController> logger)
        {
            this.env = env;
            this.logger = logger;
        }

        [HttpGet("Download/{documentName}")]
        public IActionResult DownloadDocument(string documentName)
        {
            var filePath = string.Empty;
            var contentType = "application/pdf";

            switch (documentName.ToLower())
            {
                case "anmeldeformular":
                    filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "documents", "Anmeldeformular.pdf");
                    break;
                case "satzung":
                    filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "documents", "Satzung.pdf");
                    break;
                default:
                    return NotFound("Das angeforderte Dokument wurde nicht gefunden.");
            }

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Das angeforderte Dokument wurde nicht gefunden.");
            }

            var fileName = Path.GetFileName(filePath);
            return PhysicalFile(filePath, contentType, fileName);
        }

        //[HttpGet]
        //public IActionResult GetDocument(string name)
        //{
        //    // do logic to create csv in memoryStream

        //    return new FileStreamResult(memoryStream, "text/csv;charset=utf-8")
        //    {
        //        FileDownloadName = "products.csv",
        //    };
        //}
    }
}
