using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    /// <summary>Wer eine Änderung an einer Anmeldung ausgelöst hat.</summary>
    public enum EreignisAkteur
    {
        [Description("Teilnehmer")]
        Teilnehmer = 0,

        [Description("Organisator")]
        Admin = 1,

        [Description("System")]
        System = 2
    }
}
