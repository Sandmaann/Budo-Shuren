using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class WissenKategorie
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(450)]
        public string? AbteilungId { get; set; }

        public Abteilung? Abteilung { get; set; }

        [Required]
        [MaxLength(150)]
        public string Slug { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public ICollection<WissenBeitrag> Beitraege { get; set; } = new List<WissenBeitrag>();
    }
}
