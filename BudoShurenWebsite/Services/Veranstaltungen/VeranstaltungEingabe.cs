using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using System.ComponentModel.DataAnnotations;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Formularmodell der Bearbeiten-Seite. Fachliche Regeln prüft der VeranstaltungVerwaltungService.</summary>
    public sealed class VeranstaltungEingabe
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Bitte einen Titel angeben.")]
        [MaxLength(200, ErrorMessage = "Der Titel darf höchstens 200 Zeichen lang sein.")]
        public string Titel { get; set; } = string.Empty;

        /// <summary>Leer = wird aus dem Titel erzeugt.</summary>
        [MaxLength(250)]
        public string? Slug { get; set; }

        [MaxLength(500, ErrorMessage = "Die Kurzbeschreibung darf höchstens 500 Zeichen lang sein.")]
        public string? Kurzbeschreibung { get; set; }

        /// <summary>Beschreibung als Text- und Bildbausteine, in Anzeigereihenfolge.</summary>
        public List<BlockEingabe> Bloecke { get; set; } = [];

        [MaxLength(200)]
        public string? Ort { get; set; }

        [MaxLength(300)]
        public string? Adresse { get; set; }

        [MaxLength(500)]
        [Url(ErrorMessage = "Bitte einen vollständigen Link angeben (https://...).")]
        public string? KartenLink { get; set; }

        public string? AbteilungId { get; set; }

        [MaxLength(200)]
        public string? KontaktName { get; set; }

        [MaxLength(320)]
        [EmailAddress(ErrorMessage = Messages.EmailFormatErrorMessage)]
        public string? KontaktEmail { get; set; }

        public VeranstaltungSichtbarkeit Sichtbarkeit { get; set; } = VeranstaltungSichtbarkeit.Oeffentlich;

        public DateTime? AnmeldungAb { get; set; }

        public DateTime? AnmeldungBis { get; set; }

        public DateTime? AenderungenBis { get; set; }

        public Teilnahmemodus Teilnahmemodus { get; set; } = Teilnahmemodus.NurGesamt;

        [Range(1, 30, ErrorMessage = "Mindestens 1 Termin.")]
        public int MinTageBeiTeilanmeldung { get; set; } = 1;

        [Range(1, 10000, ErrorMessage = "Die Kapazität muss mindestens 1 sein (leer = unbegrenzt).")]
        public int? MaxTeilnehmerVorgabe { get; set; }

        [Range(0, 20, ErrorMessage = "Zwischen 0 und 20 Begleitpersonen.")]
        public int MaxBegleitpersonen { get; set; } = 2;

        public bool DoubleOptIn { get; set; } = true;

        public FormularFeldModus TelefonFeld { get; set; } = FormularFeldModus.Optional;

        public FormularFeldModus VereinFeld { get; set; } = FormularFeldModus.Optional;

        public FormularFeldModus GraduierungFeld { get; set; } = FormularFeldModus.Optional;

        public FormularFeldModus BemerkungFeld { get; set; } = FormularFeldModus.Optional;

        public TimeOnly ZusammenfassungUhrzeit { get; set; } = new(7, 0);

        public List<TagEingabe> Tage { get; set; } = [];

        /// <summary>Stand beim Laden; erkennt gleichzeitige Bearbeitung.</summary>
        public byte[]? RowVersion { get; set; }

        // Nur zur Anzeige
        public VeranstaltungStatus Status { get; set; } = VeranstaltungStatus.Entwurf;

        public bool WarVeroeffentlicht { get; set; }

        /// <summary>
        /// Was Teilnehmer bei einer Änderung erfahren sollten (Plan 6.2): Ort, Adresse und Zeiten der stattfindenden Termine.
        /// Zwei Stände vergleichen: ungleich = Termin oder Ort geändert.
        /// </summary>
        public string TerminUndOrt() =>
            string.Join("|",
                new[] { Ort?.Trim() ?? "", Adresse?.Trim() ?? "" }
                    .Concat(Tage.Where(t => !t.Abgesagt)
                        .Select(t => $"{t.Datum:yyyy-MM-dd} {t.Beginn:HH\\:mm}-{t.Ende:HH\\:mm}")
                        .Order(StringComparer.Ordinal)));
    }

    /// <summary>Ein Baustein der Beschreibung. Beim Speichern werden alle Bausteine neu geschrieben (sie haben keine Verweise).</summary>
    public sealed class BlockEingabe
    {
        public VeranstaltungBlockTyp Typ { get; set; }

        public string? MarkdownInhalt { get; set; }

        [Range(1, 6, ErrorMessage = "Zwischen 1 und 6 Bilder pro Reihe.")]
        public int BilderProReihe { get; set; } = 3;

        [MaxLength(500, ErrorMessage = "Die Bildunterschrift darf höchstens 500 Zeichen lang sein.")]
        public string? BildUnterschrift { get; set; }

        /// <summary>Ids der Bilder (DbImage), in Anzeigereihenfolge.</summary>
        public List<int> BildIds { get; set; } = [];
    }

    public sealed class TagEingabe : ITermin
    {
        /// <summary>Leer = neuer Termin.</summary>
        public int? Id { get; set; }

        public DateOnly Datum { get; set; }

        public TimeOnly Beginn { get; set; } = new(10, 0);

        /// <summary>Leer = offenes Ende ("ab 19:00 Uhr").</summary>
        public TimeOnly? Ende { get; set; } = new(16, 0);

        [MaxLength(200)]
        public string? Titel { get; set; }

        [Range(1, 10000, ErrorMessage = "Die Kapazität muss mindestens 1 sein (leer = unbegrenzt).")]
        public int? MaxTeilnehmer { get; set; }

        // Nur zur Anzeige: abgesagte Termine werden über "Termin absagen" verwaltet, nicht im Formular
        public bool Abgesagt { get; set; }
    }
}
