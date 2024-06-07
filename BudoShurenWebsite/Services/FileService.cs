using Microsoft.Extensions.Hosting;
using System;

namespace BudoShurenWebsite.Services
{
    public class FileService
    {
        private readonly IWebHostEnvironment _environment;
        public FileService(IWebHostEnvironment environment)
        {
            this._environment = environment;
        }
        public string GetTempFilePath(Syncfusion.Blazor.Inputs.FileInfo? file)
        {
            if (file == null)
                throw new NullReferenceException(GetTempFilePath(null) + " is null");
            string tempFilePath = Path.Combine(_environment.ContentRootPath, "wwwroot",
                _environment.EnvironmentName, "unsafe_uploads",
                file.Name);
            return tempFilePath;
        }

        public string GetGaleryDestinationPath(Syncfusion.Blazor.Inputs.FileInfo? file)
        {
            if (file == null)
                throw new NullReferenceException("GetGaleryDestinationPath(FileInfo) is null");
            //Datei in Galerie-Ordner kopieren
            string destination = Path.Combine(_environment.ContentRootPath, "wwwroot", _environment.EnvironmentName, "galery");
            string newFileName = Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(file.Name));
            string newFilePath = Path.Combine(destination, newFileName);
            return newFilePath;
        }
        public string GetGaleryDestinationPath(string fileName)
        {
            //Datei in Galerie-Ordner kopieren
            string destination = Path.Combine(_environment.ContentRootPath, "wwwroot", _environment.EnvironmentName, "galery");
            string newFileName = Path.ChangeExtension(Path.GetRandomFileName(), Path.GetExtension(fileName));
            string newFilePath = Path.Combine(destination, newFileName);
            return newFilePath;
        }
        public async Task CopyFileAsync(string sourceFile, string destinationFile)
        {
            using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destinationStream = new FileStream(destinationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await sourceStream.CopyToAsync(destinationStream);
        }
    }
}
