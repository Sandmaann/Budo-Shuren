using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum NachrichtArt
    {
        [Description("Rundmail")]
        Rundmail = 0,

        [Description("Tag abgesagt")]
        TagAbgesagt = 1,

        [Description("Veranstaltung abgesagt")]
        VeranstaltungAbgesagt = 2
    }
}
