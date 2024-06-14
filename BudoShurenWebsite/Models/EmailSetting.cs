using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models
{
    public class EmailSetting
    {
        [Key]
        public int ID { get; set; }
        public bool IsMain { get; set; }
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string SmtpUser { get; set; }
        public string SmtpPassword { get; set; }
    }
}
