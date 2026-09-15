using System.ComponentModel.DataAnnotations;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Models
{
    public class AktuellesBlock
    {
        [Key]
        public int Id { get; set; }

        public int BeitragId { get; set; }

        public AktuellesBeitrag? Beitrag { get; set; }

        public AktuellesBlockTyp Typ { get; set; }

        public int Sortierung { get; set; }

        public string? MarkdownInhalt { get; set; }

        public int BilderProReihe { get; set; } = 3;

        [MaxLength(500)]
        public string? BildUnterschrift { get; set; }

        public ICollection<AktuellesBild> Bilder { get; set; } = new List<AktuellesBild>();
    }
}
