using System.Net;
using BudoShurenWebsite.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

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

        public FilesaveController(IHostEnvironment env,
            ILogger<FilesaveController> logger)
        {
            this.env = env;
            this.logger = logger;
        }

        [HttpPost("[action]")]
        public async Task Save(IList<IFormFile> UploadFiles)
        {
            var maxAllowedFiles = 100;
            long maxFileSize = 1024 * 1024 * 5;
            var filesProcessed = 0;

            try
            {
                foreach (var file in UploadFiles)
                {
                    if (filesProcessed < maxAllowedFiles)
                    {
                        if (file.Length == 0)
                        {
                            logger.LogInformation("{FileName} length is 0 (Err: 1)",
                                file.FileName);
                        }
                        else if (file.Length > maxFileSize)
                        {
                            logger.LogInformation("{FileName} of {Length} bytes is " +
                                "larger than the limit of {Limit} bytes (Err: 2)",
                                file.FileName, file.Length, maxFileSize);
                        }
                        else
                        {
                            string filename;
                            if (env.IsDevelopment())
                            {
                                filename = Path.Combine(env.ContentRootPath, "wwwroot", env.EnvironmentName, "unsafe_uploads",
                                    file.FileName);
                            }
                            else
                            {
                                filename = Path.Combine(env.ContentRootPath,
                                    "unsafe_uploads",                                                
                                    file.FileName);
                            }
                            if (!System.IO.File.Exists(filename))
                            {
                                //Bild komprimieren
                                await KomprimiereUndSpeichereBildAsync(file, filename);

                                //using (FileStream fs = System.IO.File.Create(filename))
                                //{
                                //    await file.CopyToAsync(fs);
                                //    await fs.FlushAsync();
                                //}
                            }
                        }
                        filesProcessed++;
                    }
                }
            }
            catch (Exception e)
            {
                Response.Clear();
                Response.StatusCode = 204;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "File failed to upload";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;
            }
        }

        [HttpPost("[action]")]
        public void Remove(IList<IFormFile> UploadFiles)
        {
            try
            {
                string filename;

                if (env.IsDevelopment())
                {
                    filename = Path.Combine(env.ContentRootPath, "wwwroot", env.EnvironmentName, "unsafe_uploads",
                        UploadFiles[0].FileName);
                }
                else
                {
                    filename = Path.Combine(env.ContentRootPath,
                        "unsafe_uploads",
                        UploadFiles[0].FileName);
                }

                //var filename = env.ContentRootPath + $@"\{UploadFiles[0].FileName}";
                if (System.IO.File.Exists(filename))
                {
                    System.IO.File.Delete(filename);
                }
            }
            catch (Exception e)
            {
                Response.Clear();
                Response.StatusCode = 200;
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = "File removed successfully";
                Response.HttpContext.Features.Get<IHttpResponseFeature>().ReasonPhrase = e.Message;
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