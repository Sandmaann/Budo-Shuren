using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum VeranstaltungStatus
    {
        [Description("Entwurf")]
        Entwurf = 0,

        [Description("Veröffentlicht")]
        Veroeffentlicht = 1,

        [Description("Abgesagt")]
        Abgesagt = 2,

        [Description("Abgeschlossen")]
        Abgeschlossen = 3,

        [Description("Archiviert")]
        Archiviert = 4
    }
}
