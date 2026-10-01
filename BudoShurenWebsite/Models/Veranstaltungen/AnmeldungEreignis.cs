using BudoShurenWebsite.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Models.Veranstaltungen
{
    /// <summary>
    /// Historie einer Anmeldung. Grundlage für das "geändert"-Badge in der Übersicht
    /// und für die Organisator-Benachrichtigungen.
    /// </summary>
    public class AnmeldungEreignis
    {
        [Key]
        public int Id { get; set; }

        public int AnmeldungId { get; set; }

        public Anmeldung? Anmeldung { get; set; }

        public DateTime ZeitpunktUtc { get; set; }

        public EreignisAkteur Akteur { get; set; }

        /// <summary>Benutzer-Id, wenn ein Organisator die Änderung gemacht hat.</summary>
        [MaxLength(450)]
        public string? AkteurUserId { get; set; }

        public AnmeldungEreignisArt Art { get; set; }

        /// <summary>Alte und neue Werte als JSON. Enthält personenbezogene Daten (wird bei Anonymisierung geleert).</summary>
        public string? DetailsJson { get; set; }
    }
}
