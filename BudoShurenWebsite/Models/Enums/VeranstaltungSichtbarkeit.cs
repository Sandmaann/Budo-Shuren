using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum VeranstaltungSichtbarkeit
    {
        [Description("Öffentlich (Liste und Kalender)")]
        Oeffentlich = 0,

        [Description("Nur über den Link erreichbar")]
        NurPerLink = 1
    }
}
