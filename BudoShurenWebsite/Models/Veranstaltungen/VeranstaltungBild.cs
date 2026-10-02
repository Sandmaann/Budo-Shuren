using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>Ein Bild einer Galerie. Die Bilddaten liegen wie überall auf der Website in DbImage.</summary>
    public class VeranstaltungBild
    {
        [Key]
        public int Id { get; set; }

        public int BlockId { get; set; }

        public VeranstaltungBlock? Block { get; set; }

        public int BildId { get; set; }

        public DbImage? Bild { get; set; }

        public int Sortierung { get; set; }
    }
}
