using System.IO;
using System.Net;
using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace BudoShurenWebsite.Controllers
{
    [Authorize]
    [Authorize(Policy = "NotGuest")]
    [Authorize(Policy = "Aktiviert")]
    [Route("Account/Member/[controller]")]
    [ApiController]
    public class FilesaveController : ControllerBase
    {
        private readonly IHostEnvironment env;
        private readonly ILogger<FilesaveController> logger;
        private readonly int maximaleBildBreite = 1920;
        private readonly int maximaleBildHoehe = 1080;

        /// <summary>
        /// So lange darf der Browser ein öffentliches Bild behalten, ohne nachzufragen (7 Tage).
        /// Wird ein Bild nicht mehr öffentlich gezeigt, bleibt es höchstens so lange bei Besuchern, die es schon gesehen haben.
        /// </summary>
        private const int OeffentlichCacheSekunden = 7 * 24 * 60 * 60;
        //private readonly IOptions<FileSystemOptions> fileSystemOptions;
        private readonly ImageService imageService;
        private readonly BildVariantenService bildVarianten;

        public FilesaveController(IHostEnvironment env,
            ImageService imageService,
            BildVariantenService bildVarianten,
            ILogger<FilesaveController> logger)
        {
            this.env = env;
            this.logger = logger;
            this.imageService = imageService;
            this.bildVarianten = bildVarianten;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="UploadFiles">ACHTUNG: dieser Name MUSS die ID des SfUploaders sein!!</param>
        /// <param name="vorlaeufig">true bei Editoren, die erst beim Speichern übernehmen (Aktuelles): nie gespeicherte Bilder räumt BildAufraeumJob auf.</param>
        /// <returns></returns>
        [HttpPost("[action]")]
        public async Task<IActionResult> Save(IList<IFormFile> UploadFiles, [FromQuery] bool vorlaeufig = false)
        {
            var maxAllowedFiles = 100;
            long maxFileSize = 1024 * 1024 * 5;
            var filesProcessed = 0;
            var uploadedFiles = new List<object>();

            try
            {
                foreach (var file in UploadFiles)
                {
                    if (filesProcessed < maxAllowedFiles)
                    {
                        if (file.Length == 0)
                        {
                            logger.LogInformation("{FileName} length is 0 (Err: 1)", file.FileName);
                        }
                        else if (file.Length > maxFileSize)
                        {
                            logger.LogInformation("{FileName} of {Length} bytes is larger than the limit of {Limit} bytes (Err: 2)", file.FileName, file.Length, maxFileSize);
                        }
                        else
                        {
                            try
                            {
                                var fileName = Path.GetFileName(file.FileName);
                                var contentType = file.ContentType;

                                await using (var stream = file.OpenReadStream())
                                {
                                    var imageData = await BildKomprimierung.AlsJpegAsync(stream);
                                    var imageId = await imageService.UploadImageAsync(fileName, imageData, contentType, vorlaeufig);
                                    uploadedFiles.Add(new { FileName = fileName, Id = imageId });
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, $"Error on Image-Upload File {file.Name}");
                                var errorResponse = new
                                {
                                    Success = false,
                                    Message = "File upload failed: " + ex.Message
                                };
                                return BadRequest(errorResponse);
                            }
                            filesProcessed++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error on Image-Upload");
                var errorResponse = new
                {
                    Success = false,
                    Message = "File upload failed: " + ex.Message
                };
                return BadRequest(errorResponse);
            }

            var successResponse = new
            {
                Success = true,
                Files = uploadedFiles
            };

            return Ok(successResponse);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Remove(IList<IFormFile> UploadFiles)
        {
            try
            {
                var results = new List<bool>();
                foreach (var file in UploadFiles)
                {
                    var fileName = file.FileName;
                    var contentType = file.ContentType;
                    var result = await imageService.RemoveImageAsync(fileName, contentType);
                    if(!result)
                    {
                        logger.LogWarning($"File {fileName} could not be removed");
                    }
                    results.Add(result);
                }
                if (results.Any(x => !x))
                {
                    var errorResponse = new
                    {
                        Success = false,
                        Message = $"File upload failed for {results.Where(x => !x).Count()} file(s)"
                    };
                    return BadRequest(errorResponse);
                }
                else
                {
                    return Ok();
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error on Image-Remove");
                var errorResponse = new
                {
                    Success = false,
                    Message = "File upload failed: " + e.Message
                };
                return BadRequest(errorResponse);
            }
        }

        [AllowAnonymous]
        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> GetImage(int id, [FromQuery] BildVariantenArt? variante = null)
        {
            try
            {
                if (variante is { } unbekannt && !Enum.IsDefined(unbekannt))
                {
                    return BadRequest();
                }

                var oeffentlich = await imageService.AllowAnonymous(id);
                if (!oeffentlich)
                {
                    // Wenn das Bild nicht öffentlich ist, überprüfe die Benutzerberechtigungen
                    if (User.Identity?.IsAuthenticated != true)
                    {
                        return Unauthorized();
                    }
                    // Hier kannst du zusätzliche Berechtigungsprüfungen durchführen
                    // Beispiel: Überprüfen, ob der Benutzer eine bestimmte Rolle hat
                    if (User.IsInRole("Gast"))
                    {
                        return Forbid();
                    }
                }

                var kopf = await imageService.GetBildKopfAsync(id);
                if (kopf == null)
                {
                    return NotFound();
                }

                // Die Daten zu einer Bild-Id ändern sich nie (ein neuer Upload bekommt eine neue Id),
                // deshalb genügen Id und Anlagezeit als ETag – ohne die Bilddaten zu laden.
                var etag = new EntityTagHeaderValue($"\"{id}-{kopf.CreatedAt.Ticks:x}{(variante is { } art ? $"-v{(int)art}" : "")}\"");

                // Nicht öffentliche Bilder fragt der Browser jedes Mal nach, damit die Berechtigung erneut geprüft wird.
                var cacheControl = oeffentlich
                    ? $"public, max-age={OeffentlichCacheSekunden}"
                    : "private, no-cache";

                // Feste Regel: Bilder der Galerie (und interne Bilder) nicht in die Bildersuche aufnehmen.
                // Bilder von Neuigkeiten, Themen und Veranstaltungen bleiben auffindbar.
                if (!oeffentlich || await imageService.IstGalerieBildAsync(id))
                {
                    Response.Headers["X-Robots-Tag"] = "noindex";
                }

                if (Request.GetTypedHeaders().IfNoneMatch.Any(x => x.Compare(etag, useStrongComparison: false)))
                {
                    Response.Headers.CacheControl = cacheControl;
                    Response.Headers.ETag = etag.ToString();
                    return StatusCode(StatusCodes.Status304NotModified);
                }

                // Verkleinerte Fassung, falls verlangt; lässt sie sich nicht erzeugen, das Original
                var fassung = variante is { } gewuenscht ? await bildVarianten.VarianteAsync(id, gewuenscht, HttpContext.RequestAborted) : null;
                var daten = fassung?.Daten ?? await imageService.GetImageDataAsync(id);
                if (daten == null)
                {
                    return NotFound();
                }
                // Erst jetzt setzen: eine Fehlerantwort darf der Browser nicht zwischenspeichern
                Response.Headers.CacheControl = cacheControl;
                return File(daten, fassung?.ContentType ?? kopf.ContentType, lastModified: null, entityTag: etag);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving image");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task KomprimiereUndSpeichereBildAsync(IFormFile file, string zielPfad)
        {
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                using (var bild = Image.Load(memoryStream))
                {
                    bild.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = new Size(maximaleBildBreite, maximaleBildHoehe)
                    }));

                    var encoder = new JpegEncoder
                    {
                        Quality = 90 // Qualitätseinstellung hier anpassen
                    };

                    await using (var outputStream = new FileStream(zielPfad, FileMode.Create))
                    {
                        await bild.SaveAsJpegAsync(outputStream, encoder);
                    }
                }
            }
        }
    }
}