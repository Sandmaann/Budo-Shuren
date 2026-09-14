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
        //private readonly IOptions<FileSystemOptions> fileSystemOptions;
        private readonly ImageService imageService;

        public FilesaveController(IHostEnvironment env,
            ImageService imageService,
            ILogger<FilesaveController> logger)
        {
            this.env = env;
            this.logger = logger;
            this.imageService = imageService;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="UploadFiles">ACHTUNG: dieser Name MUSS die ID des SfUploaders sein!!</param>
        /// <returns></returns>
        [HttpPost("[action]")]
        public async Task<IActionResult> Save(IList<IFormFile> UploadFiles)
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

                                using (var stream = new MemoryStream())
                                {
                                    await file.OpenReadStream().CopyToAsync(stream);
                                    stream.Seek(0, SeekOrigin.Begin);

                                    using (var bild = Image.Load(stream))
                                    {
                                        if (bild.Height > maximaleBildHoehe || bild.Width > maximaleBildBreite)
                                        {
                                            bild.Mutate(x => x.Resize(new ResizeOptions
                                            {
                                                Mode = ResizeMode.Max,
                                                Size = new Size(maximaleBildBreite, maximaleBildHoehe)
                                            }));
                                        }

                                        var encoder = new JpegEncoder
                                        {
                                            Quality = CalculateQualityByDimensions(bild.Width, bild.Height)
                                        };

                                        await using (var outputStream = new MemoryStream())
                                        {
                                            await bild.SaveAsync(outputStream, encoder);
                                            var imageData = outputStream.ToArray();
                                            var imageId = await imageService.UploadImageAsync(fileName, imageData, contentType);
                                            uploadedFiles.Add(new { FileName = fileName, Id = imageId });
                                        }
                                    }
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

        // Und füge diese Helper-Methode hinzu:
        private int CalculateQualityByDimensions(int width, int height)
        {
            var pixelCount = (long)width * height;

            // Je größer das Bild, desto stärker komprimieren
            return pixelCount switch
            {
                > 2000000 => 70,      // Großes Bild (z.B. 1920x1080) → 70%
                > 1000000 => 75,      // Mittleres Bild → 75%
                > 500000 => 85,      // Kleineres Bild → 85%
                _ => 90               // Sehr kleines Bild → 90%
            };
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
        public async Task<IActionResult> GetImage(int id)
        {
            try
            {
                if(!await imageService.AllowAnonymous(id))
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

                var image = await imageService.GetImageAsync(id);
                if (image == null)
                {
                    return NotFound();
                }
                return File(image.ImageData, image.ContentType);
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