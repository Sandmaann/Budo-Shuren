namespace BudoShurenWebsite.Models
{

    public class UploadResponse
    {
        public bool Success { get; set; }
        public List<UploadedFile> Files { get; set; } = new List<UploadedFile>();
    }
}
