namespace BudoShurenWebsite.Services
{

    public interface IImageUploadService
    {
        public Task<string> UploadImageAsync(IFormFile imageFile);
    }
    public class ImageUploadService : IImageUploadService
    {
        private readonly string _uploadDirectory;

        public ImageUploadService(IConfiguration configuration)
        {
            _uploadDirectory = configuration["UploadDirectory"] ?? throw new ArgumentNullException(nameof(configuration), "Upload directory path is not specified in configuration");
        }

        public async Task<string> UploadImageAsync(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                throw new ArgumentException("Invalid image file");

            // Generate a unique file name for the uploaded image
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
            var filePath = Path.Combine(_uploadDirectory, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return fileName; // Return the file name (or full path) for later retrieval
        }
    }
}
