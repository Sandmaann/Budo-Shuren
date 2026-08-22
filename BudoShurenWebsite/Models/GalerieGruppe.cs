namespace BudoShurenWebsite.Models
{
    public class GalerieGruppe
    {

        public string ID { get; set; } = Guid.NewGuid().ToString();

        private string? titel;
        public string? Titel
        {
            get
            {
                if (string.IsNullOrWhiteSpace(titel))
                {
                    if (Datum.Year == DateTime.Today.Year)
                        return Datum.ToString("MMMM");
                    else
                        return Datum.ToString("MMMM yyyy");
                }
                else
                    return titel;
            }
            set
            {
                titel = value;
            }
        }

        public bool DisableSelection { get; set; } = false;
        public bool DisableHover { get; set; } = false;

        public ICollection<GalerieEintrag> Items { get; set; } = new List<GalerieEintrag>();

        public DateTime Datum { get; set; }
    }
}
