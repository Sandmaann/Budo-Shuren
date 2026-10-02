using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>
    /// Ein Termin einer Veranstaltung (Ortszeit). Ein Datum kann mehrere Termine haben, z. B. Training am Tag und Essen am Abend.
    /// Kapazität und Teilanmeldung gelten pro Termin. Der Name "Tag" stammt aus der ersten Version (ein Termin pro Datum).
    /// </summary>
    public class VeranstaltungsTag : ITermin
    {
        [Key]
        public int Id { get; set; }

        public int VeranstaltungId { get; set; }

        public Veranstaltung? Veranstaltung { get; set; }

        public DateOnly Datum { get; set; }

        public TimeOnly Beginn { get; set; }

        /// <summary>null = offenes Ende ("ab 19:00 Uhr").</summary>
        public TimeOnly? Ende { get; set; }

        /// <summary>Optionaler Titel bzw. Programm des Termins, z. B. "Training" oder "Gemeinsames Essen".</summary>
        [MaxLength(200)]
        public string? Titel { get; set; }

        /// <summary>Höchstzahl Personen (inkl. Begleitpersonen). Leer = unbegrenzt.</summary>
        public int? MaxTeilnehmer { get; set; }

        /// <summary>Abgesagte Termine bleiben erhalten (Anmeldungen verweisen darauf), zählen aber nicht mehr.</summary>
        public bool Abgesagt { get; set; }

        public ICollection<AnmeldungTag> AnmeldungTage { get; set; } = new List<AnmeldungTag>();
    }
}
