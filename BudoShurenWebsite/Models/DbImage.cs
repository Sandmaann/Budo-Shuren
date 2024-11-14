using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class DbImage
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; }
        public byte[] ImageData { get; set; }
        public string ContentType { get; set; }
        public DateTime CreatedAt { get; set; }


        //// Navigation properties
        //public ICollection<GalerieEintrag> GalerieEinträge { get; set; } = new List<GalerieEintrag>();

        // Navigation property for one-to-one relationship
        public GalerieEintrag? GalerieEintrag { get; set; }

        // Navigation property for one-to-one relationship
        public Neuigkeit? Neuigkeit { get; set; }
    }
}
