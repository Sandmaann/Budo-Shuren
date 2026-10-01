using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum BenachrichtigungModus
    {
        [Description("Sofort")]
        Sofort = 0,

        [Description("Tägliche Zusammenfassung")]
        TaeglicheZusammenfassung = 1,

        [Description("Pausiert")]
        Pausiert = 2
    }
}
