using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum AnmeldungQuelle
    {
        [Description("Anmeldeformular")]
        Formular = 0,

        [Description("Manuell durch Organisator")]
        Admin = 1
    }
}
