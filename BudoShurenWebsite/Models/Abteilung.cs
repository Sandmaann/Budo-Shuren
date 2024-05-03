namespace BudoShurenWebsite.Models
{
    public class Abteilung
    {
        public string? ID { get; set; }
        public string? Name { get; set; }
        public string? Abteilungsleiter { get; set; }

        public static string Bujinkan => "Bujinkan";
        public static string Aikido => "Aikido";
        public static string Genbukan => "Genbukan";
    }
}
