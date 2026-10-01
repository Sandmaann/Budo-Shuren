using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>Ein Tag einer Veranstaltung (Ortszeit). Kapazität wird pro Tag gezählt.</summary>
    public class VeranstaltungsTag
    {
        [Key]
        public int Id { get; set; }

        public int VeranstaltungId { get; set; }

        public Veranstaltung? Veranstaltung { get; set; }

        public DateOnly Datum { get; set; }

        public TimeOnly Beginn { get; set; }

        public TimeOnly Ende { get; set; }

        /// <summary>Optionaler Titel bzw. Programm des Tages.</summary>
        [MaxLength(200)]
        public string? Titel { get; set; }

        /// <summary>Höchstzahl Personen (inkl. Begleitpersonen). Leer = unbegrenzt.</summary>
        public int? MaxTeilnehmer { get; set; }

        /// <summary>Abgesagte Tage bleiben erhalten (Anmeldungen verweisen darauf), zählen aber nicht mehr.</summary>
        public bool Abgesagt { get; set; }

        public ICollection<AnmeldungTag> AnmeldungTage { get; set; } = new List<AnmeldungTag>();
    }
}
