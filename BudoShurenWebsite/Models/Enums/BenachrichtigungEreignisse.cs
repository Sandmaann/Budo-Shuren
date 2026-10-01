using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    /// <summary>Worüber ein Empfänger von Organisator-Benachrichtigungen informiert werden will (kombinierbar).</summary>
    [Flags]
    public enum BenachrichtigungEreignisse
    {
        Keine = 0,

        [Description("Neue Anmeldung")]
        NeueAnmeldung = 1,

        [Description("Abmeldung")]
        Abmeldung = 2,

        [Description("Änderung einer Anmeldung")]
        Aenderung = 4,

        [Description("Veranstaltung ausgebucht")]
        Ausgebucht = 8,

        [Description("Anmeldeschluss erreicht")]
        AnmeldeschlussErreicht = 16,

        Alle = NeueAnmeldung | Abmeldung | Aenderung | Ausgebucht | AnmeldeschlussErreicht
    }
}
