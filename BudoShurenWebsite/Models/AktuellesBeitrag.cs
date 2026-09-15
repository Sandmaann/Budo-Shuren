using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class AktuellesBeitrag
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Titel { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(450)]
        public string? AbteilungId { get; set; }

        public Abteilung? Abteilung { get; set; }

        public bool Veroeffentlicht { get; set; }

        public DateTime Datum { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(200)]
        public string MetaTitel { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string MetaBeschreibung { get; set; } = string.Empty;

        [MaxLength(450)]
        public string ErstelltVon { get; set; } = string.Empty;

        public DateTime? Erstellt { get; set; }

        [MaxLength(450)]
        public string GeaendertVon { get; set; } = string.Empty;

        public DateTime? Geaendert { get; set; }

        public ICollection<AktuellesBlock> Bloecke { get; set; } = new List<AktuellesBlock>();
    }
}
