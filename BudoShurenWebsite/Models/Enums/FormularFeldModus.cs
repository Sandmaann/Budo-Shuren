using System.ComponentModel;

namespace BudoShurenWebsite.Models.Enums
{
    /// <summary>Ob ein optionales Feld im Anmeldeformular erscheint und ob es Pflicht ist.</summary>
    public enum FormularFeldModus
    {
        [Description("Nicht abfragen")]
        Aus = 0,

        [Description("Optional")]
        Optional = 1,

        [Description("Pflichtfeld")]
        Pflicht = 2
    }
}
