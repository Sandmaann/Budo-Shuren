using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class AktuellesBild
    {
        [Key]
        public int Id { get; set; }

        public int BlockId { get; set; }

        public AktuellesBlock? Block { get; set; }

        public int BildId { get; set; }

        public DbImage? Bild { get; set; }

        public int Sortierung { get; set; }
    }
}
