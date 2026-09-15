using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Models
{
    public class WissenBlock
    {
        [Key]
        public int Id { get; set; }

        public int BeitragId { get; set; }

        public WissenBeitrag? Beitrag { get; set; }

        public WissenBlockTyp Typ { get; set; }

        public int Sortierung { get; set; }

        public string? TextInhalt { get; set; }

        [MaxLength(200)]
        public string? UntertitelText { get; set; }

        public int? BildId { get; set; }

        public DbImage? Bild { get; set; }

        [MaxLength(300)]
        public string? AltText { get; set; }

        [MaxLength(10)]
        public string BildPosition { get; set; } = "links";

        public string? ListenItemsJson { get; set; }

        // Optionaler SectionBanner am Anfang jedes Blocks
        [MaxLength(200)]
        public string? BannerTitel { get; set; }

        public bool BannerAusblenden { get; set; } = false;

        // Zweiter Textbereich (z. B. für UeberschriftZweiTexte)
        public string? TextInhalt2 { get; set; }
    }
}
