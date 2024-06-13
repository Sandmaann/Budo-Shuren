using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudoShurenWebsite.Models
{
    [Flags]
    public enum GalerieTyp
    {
        None = 0,
        Home = 1,
        Aikido = 2,
        Bujinkan = 4,
        Genbukan = 8,
        Iaido = 16,
        Jodo = 32
    }

    public class GalerieEintrag
    {
        [Key]
        public int ID { get; set; }
        public string? Titel { get; set; }
        public string? Untertitel { get; set; }
        public string? Beschreibung { get; set; }

        public bool Öffentlich { get; set; }
        public bool Home { get; set; }
        public bool Aikido { get; set; }
        public bool Bujinkan { get; set; }
        public bool Genbukan { get; set; }
        public bool Iaido { get; set; }
        public bool Jodo { get; set; }

        public string? ImagePath { get; set; }

        public DateTime? ImageDate { get; set; }
        public string? ImageCreatedBy { get; set; }


        public DateTime? EntryCreationDateUTC { get; set; }
        public string? EntryCreatedBy { get; set; }

        public DateTime? LastChangedUTC { get; set; }
        public string? LastChangedBy { get; set; }

        [NotMapped]
        public DateTime? SortDate => ImageDate ?? EntryCreationDateUTC;

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

        [NotMapped]
        public bool IsSelected { get; set; }

        [NotMapped]
        public bool SelectionMode { get; set; }

        [NotMapped]
        public GalerieTyp Typ
        {
            get
            {
                GalerieTyp typ = GalerieTyp.None;

                if(Home)
                    typ |= GalerieTyp.Home;
                if(Aikido)
                    typ |= GalerieTyp.Aikido;
                if (Bujinkan)
                    typ |= GalerieTyp.Bujinkan;
                if (Genbukan)
                    typ |= GalerieTyp.Genbukan;
                if (Iaido)
                    typ |= GalerieTyp.Iaido;
                if (Jodo)
                    typ |= GalerieTyp.Jodo;
                return typ;
            }
        }
        public bool IsValid()
        {
            if (!string.IsNullOrWhiteSpace(ImagePath))
            {
                if (File.Exists(ImagePath))
                    return true;
            }
            return false;
        }
    }
}
