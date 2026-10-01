using BudoShurenWebsite.Services.Veranstaltungen;

namespace BudoShurenWebsite.Components.Pages.Veranstaltungen
{
    /// <summary>Formularmodell der öffentlichen Anmeldung (statisches Rendern, [SupplyParameterFromForm]).</summary>
    public sealed class AnmeldeFormular
    {
        public AnmeldeEingabe Eingabe { get; set; } = new();

        /// <summary>
        /// Je Tag ein Eintrag mit verstecktem Id-Feld: so sind die Indizes beim Absenden lückenlos,
        /// auch wenn nicht jeder Tag angehakt ist (Voraussetzung für die Formularbindung von Listen).
        /// </summary>
        public List<TagWahl> Tage { get; set; } = [];

        /// <summary>Honeypot: für Menschen unsichtbar, Bots füllen es aus.</summary>
        public string? Website { get; set; }

        /// <summary>Verschlüsselter Ladezeitpunkt (FormularSchutz).</summary>
        public string? Zeitstempel { get; set; }
    }

    public sealed class TagWahl
    {
        public int Id { get; set; }

        public bool Gewaehlt { get; set; }
    }
}
