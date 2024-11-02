namespace BudoShurenWebsite.Models
{
    public class UploadedFile
    {
        public string FileName { get; set; } = string.Empty;
        public int Id { get; set; }

        public bool EntryCreated { get; set; }
    }
}
