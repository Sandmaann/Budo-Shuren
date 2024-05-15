using System.Net;
using BudoShurenWebsite.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using static BudoShurenWebsite.Components.Account.Pages.Member.Galerie.EditGalerie;

namespace BudoShurenWebsite.Controllers
{
    [Route("Account/Member/[controller]")]
    [ApiController]
    public class FilesaveController : ControllerBase
    {
        private readonly IHostEnvironment env;
        private readonly ILogger<FilesaveController> logger;

        public FilesaveController(IHostEnvironment env,
            ILogger<FilesaveController> logger)
        {
            this.env = env;
            this.logger = logger;
        }

        //ASP Net Variante

        //[HttpPost]
        //public async Task<ActionResult<IList<UploadResult>>> PostFile(
        //    [FromForm] IEnumerable<IFormFile> files)
        //{
        //    var maxAllowedFiles = 100;
        //    long maxFileSize = 1024 * 1024;
        //    var filesProcessed = 0;
        //    var resourcePath = new Uri($"{Request.Scheme}://{Request.Host}/");
        //    List<UploadResult> uploadResults = new();

        //    foreach (var file in files)
        //    {
        //        var uploadResult = new UploadResult();
        //        string trustedFileNameForFileStorage;
        //        var untrustedFileName = file.FileName;
        //        uploadResult.FileName = untrustedFileName;
        //        var trustedFileNameForDisplay =
        //            WebUtility.HtmlEncode(untrustedFileName);

        //        if (filesProcessed < maxAllowedFiles)
        //        {
        //            if (file.Length == 0)
        //            {
        //                logger.LogInformation("{FileName} length is 0 (Err: 1)",
        //                    trustedFileNameForDisplay);
        //                uploadResult.ErrorCode = 1;
        //            }
        //            else if (file.Length > maxFileSize)
        //            {
        //                logger.LogInformation("{FileName} of {Length} bytes is " +
        //                    "larger than the limit of {Limit} bytes (Err: 2)",
        //                    trustedFileNameForDisplay, file.Length, maxFileSize);
        //                uploadResult.ErrorCode = 2;
        //            }
        //            else
        //            {
        //                try
        //                {
        //                    trustedFileNameForFileStorage = Path.GetRandomFileName();
        //                    var path = Path.Combine(env.ContentRootPath,
        //                        env.EnvironmentName, "unsafe_uploads",
        //                        trustedFileNameForFileStorage);

        //                    await using FileStream fs = new(path, FileMode.Create);
        //                    await file.CopyToAsync(fs);

        //                    logger.LogInformation("{FileName} saved at {Path}",
        //                        trustedFileNameForDisplay, path);
        //                    uploadResult.Uploaded = true;
        //                    uploadResult.StoredFileName = trustedFileNameForFileStorage;
        //                }
        //                catch (IOException ex)
        //                {
        //                    logger.LogError("{FileName} error on upload (Err: 3): {Message}",
        //                        trustedFileNameForDisplay, ex.Message);
        //                    uploadResult.ErrorCode = 3;
        //                }
        //            }

        //            filesProcessed++;
        //        }
        //        else
        //        {
        //            logger.LogInformation("{FileName} not uploaded because the " +
        //                "request exceeded the allowed {Count} of files (Err: 4)",
        //                trustedFileNameForDisplay, maxAllowedFiles);
        //            uploadResult.ErrorCode = 4;
        //        }

        //        uploadResults.Add(uploadResult);
        //    }

        //    return new CreatedResult(resourcePath, uploadResults);
        //}


        //Syncfusion FileUpload
        //public async Task<ActionResult<IList<UploadResult>>> PostFile(

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
                            //filename = Path.Combine(env.ContentRootPath,
                            //    env.EnvironmentName, "unsafe_uploads",
                            //    file.FileName);

                            //var test = Path.GetTempPath();

                            //var filename = env.ContentRootPath + $@"\{file.FileName}";
                            if (!System.IO.File.Exists(filename))
                            {
                                using (FileStream fs = System.IO.File.Create(filename))
                                {
                                    await file.CopyToAsync(fs);
                                    await fs.FlushAsync();
                                }
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
    }
}
