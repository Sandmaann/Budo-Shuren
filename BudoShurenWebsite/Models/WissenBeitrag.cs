using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class WissenBeitrag
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Titel { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Slug { get; set; } = string.Empty;

        public int KategorieId { get; set; }

        public WissenKategorie? Kategorie { get; set; }

        [Required]
        [MaxLength(200)]
        public string MetaTitel { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string MetaBeschreibung { get; set; } = string.Empty;

        public bool Veroeffentlicht { get; set; }
        public bool AbteilungLink { get; set; }

        public int SortOrder { get; set; }

        [MaxLength(450)]
        public string ErstelltVon { get; set; } = string.Empty;

        public DateTime? Erstellt { get; set; }

        [MaxLength(450)]
        public string GeaendertVon { get; set; } = string.Empty;

        public DateTime? Geaendert { get; set; }

        /// <summary>
        /// Großes Kanji / Zeichen das in der Headline groß angezeigt wird. Bitte korrektes Kanji verwenden.
        /// </summary>
        [MaxLength(20)]
        public string HeadlineTitel { get; set; } = "知識";

        /// <summary>
        /// Kleiner Zusatztext unten rechts in der Headline (z. B. Stil, Zitat, Kategorie-Begriff auf Japanisch).
        /// Bitte korrektes Kanji/Kana verwenden.
        /// </summary>
        [MaxLength(100)]
        public string HeadlineAnekdote { get; set; } = string.Empty;

        public ICollection<WissenBlock> Bloecke { get; set; } = new List<WissenBlock>();
    }
}
