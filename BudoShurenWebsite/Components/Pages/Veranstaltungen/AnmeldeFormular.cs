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

    /// <summary>Änderungsformular auf "Meine Anmeldung" (über den Verwaltungslink, daher ohne Honeypot).</summary>
    public sealed class AenderungsFormular
    {
        public AnmeldeEingabe Eingabe { get; set; } = new();

        public List<TagWahl> Tage { get; set; } = [];
    }

    /// <summary>Abmelden mit ausdrücklicher Bestätigung (Haken).</summary>
    public sealed class AbmeldeFormular
    {
        public bool Bestaetigt { get; set; }
    }

    /// <summary>
    /// Für Seiten hinter einem Link, auf denen nur ein Knopf etwas auslöst (Bestätigen, Info abmelden usw.):
    /// das Öffnen des Links (GET) ändert nichts, erst das Absenden (POST).
    /// </summary>
    public sealed class KnopfFormular
    {
        public string? Knopf { get; set; }
    }

    /// <summary>Link anfordern: nur die Adresse, dazu Honeypot und Zeitstempel (FormularSchutz).</summary>
    public sealed class LinkAnfordernFormular
    {
        public string? Email { get; set; }

        public string? Website { get; set; }

        public string? Zeitstempel { get; set; }
    }
}
