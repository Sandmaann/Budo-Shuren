namespace BudoShurenWebsite.Models
{
    public class GalerieGruppe
    {

        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string Titel
        {
            get
            {
                if(Datum.Year == DateTime.Today.Year)
                    return Datum.ToString("MMMM");
                else
                    return Datum.ToString("MMMM yyyy");
            }
        }
        public ICollection<GalerieEintrag> Items { get; set; } = new List<GalerieEintrag>();

        public DateTime Datum { get; set; }
    }
}
