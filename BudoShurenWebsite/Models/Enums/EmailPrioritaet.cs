using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    /// <summary>Kleinerer Wert = wird zuerst versendet.</summary>
    public enum EmailPrioritaet
    {
        [Description("Hoch (z. B. Bestätigungslinks)")]
        Hoch = 0,

        [Description("Normal (z. B. Rundmails)")]
        Normal = 1
    }
}
