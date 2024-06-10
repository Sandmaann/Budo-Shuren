using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudoShurenWebsite.Models
{
    public class Neuigkeit
    {
        [Key]
        public int ID { get; set; }
        public string Titel { get; set; }
        public string Beschreibung { get; set; }
        public string Ort { get; set; }
        public string Link { get; set; }
        public string Linktext { get; set; }
        public DateTime? Datum { get; set; }
        public int Sortierung { get; set; }

        public string ImagePath { get; set; }


        public DateTime? Created { get; set; }
        public string EntryCreatedBy { get; set; }

        public string LastChangedBy { get; set; }
        public DateTime? LastChange { get; set; }


        [NotMapped]
        public string TempFilePath { get; set; }


        [NotMapped]
        public string DisplayPath
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ImagePath) && !string.IsNullOrWhiteSpace(EnvironmentPath) && File.Exists(ImagePath))
                {
                    string path = Path.Combine(EnvironmentPath, "wwwroot");
                    var result = ImagePath.Replace(path, string.Empty);
                    return result;
                }
                return string.Empty;
            }
        }

        [NotMapped]
        public string EnvironmentPath { get; set; }
    }
}
