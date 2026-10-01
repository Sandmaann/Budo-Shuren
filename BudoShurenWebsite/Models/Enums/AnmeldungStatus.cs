using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    public enum AnmeldungStatus
    {
        [Description("Unbestätigt (E-Mail noch nicht bestätigt)")]
        Unbestaetigt = 0,

        [Description("Angemeldet")]
        Angemeldet = 1,

        [Description("Warteliste")]
        Warteliste = 2,

        [Description("Abgemeldet")]
        Storniert = 3,

        [Description("Abgelehnt")]
        Abgelehnt = 4
    }
}
