using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>Ein Baustein der Beschreibung: Markdown-Text oder Bildergalerie, in der Reihenfolge von Sortierung.</summary>
    public class VeranstaltungBlock
    {
        [Key]
        public int Id { get; set; }

        public int VeranstaltungId { get; set; }

        public Veranstaltung? Veranstaltung { get; set; }

        public VeranstaltungBlockTyp Typ { get; set; }

        public int Sortierung { get; set; }

        /// <summary>Nur bei MarkdownText; wird ohne HTML gerendert (MarkdownText.SicherZuHtml).</summary>
        public string? MarkdownInhalt { get; set; }

        /// <summary>Nur bei BilderGalerie: Spalten ab Tablet-Breite (1–6); auf dem Handy höchstens 2.</summary>
        public int BilderProReihe { get; set; } = 3;

        [MaxLength(500)]
        public string? BildUnterschrift { get; set; }

        public ICollection<VeranstaltungBild> Bilder { get; set; } = new List<VeranstaltungBild>();
    }
}
