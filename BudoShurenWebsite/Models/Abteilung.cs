using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class Abteilung
    {
        [Key]
        public string? ID { get; set; }
        public string? Name { get; set; }
        public string? Abteilungsleiter { get; set; }
        public string? Email { get; set; }


        public string? Kontaktperson { get; set; }
        public string? Telefon { get; set; }
        public int SortOrder { get; set; }

        public static string Bujinkan => "Bujinkan";
        public static string Aikido => "Aikido";
        public static string Genbukan => "Genbukan";

        /// <summary>
        /// Passt ein gespeicherter Abteilungswert (z. B. ApplicationUser.Abteilung) zu dieser Abteilung?
        /// Je nach Stelle steht dort die Id oder der Name; in Produktion sind beide gleich, in anderen Datenbanken nicht.
        /// </summary>
        public bool Passt(string? wert) =>
            !string.IsNullOrWhiteSpace(wert)
            && (string.Equals(wert, ID, StringComparison.OrdinalIgnoreCase) || string.Equals(wert, Name, StringComparison.OrdinalIgnoreCase));
    }
}
