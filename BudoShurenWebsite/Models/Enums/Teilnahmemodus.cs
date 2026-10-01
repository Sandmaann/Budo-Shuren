using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum Teilnahmemodus
    {
        [Description("Anmeldung nur für die gesamte Veranstaltung")]
        NurGesamt = 0,

        [Description("Anmeldung auch für einzelne Tage")]
        EinzelneTage = 1
    }
}
