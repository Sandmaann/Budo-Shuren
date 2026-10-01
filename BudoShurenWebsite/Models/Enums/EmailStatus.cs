using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum EmailStatus
    {
        [Description("Wartet auf Versand")]
        Wartend = 0,

        [Description("Versendet")]
        Gesendet = 1,

        [Description("Endgültig fehlgeschlagen")]
        Fehlgeschlagen = 2
    }
}
