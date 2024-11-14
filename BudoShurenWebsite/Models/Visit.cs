namespace BudoShurenWebsite.Models
{
    public class Visit
    {
        public int ID { get; set; }
        public string VisitorID { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
