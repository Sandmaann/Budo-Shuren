namespace BudoShurenWebsite.Data
{
    public class GalerieEintrag
    {
        public int ID { get; set; }
        public string? Titel { get; set; }
        public string? Untertitel { get; set; }
        public string? Beschreibung { get; set; }

        public bool Home { get; set; }
        public bool Aikido { get; set; }
        public bool Bujinkan { get; set; }
        public bool Genbukan { get; set; }

        public string? ImagePath { get; set; }

        public DateTime? ImageDate { get; set; }
        public string? ImageCreatedBy { get; set; }


        public DateTime? EntryCreationDateUTC { get; set; }
        public string? EntryCreatedBy { get; set; }


        public bool IsValid()
        {
            if(!string.IsNullOrWhiteSpace(ImagePath))
            {
                if (File.Exists(ImagePath))
                    return true;
            }
            return false;
        }

        //Erstelle eine Funktion zum Upload von Dokumenten aus ImagePath

    }
}
