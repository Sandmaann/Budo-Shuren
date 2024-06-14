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

        public static string Bujinkan => "Bujinkan";
        public static string Aikido => "Aikido";
        public static string Genbukan => "Genbukan";
    }
}
