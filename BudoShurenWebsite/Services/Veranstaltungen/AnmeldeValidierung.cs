using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models.Enums;
using BudoShurenWebsite.Models.Veranstaltungen;
using BudoShurenWebsite.Services.Mail;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Formularmodell der öffentlichen Anmeldung. Welche Felder Pflicht sind, bestimmt die Veranstaltung.</summary>
    public sealed class AnmeldeEingabe
    {
        public string? Vorname { get; set; }
        public string? Nachname { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public string? Verein { get; set; }
        public string? Graduierung { get; set; }
        public string? Bemerkung { get; set; }
        public int AnzahlBegleitpersonen { get; set; }

        /// <summary>Gebuchte Tage, nur bei Teilnahmemodus EinzelneTage.</summary>
        public List<int> TagIds { get; set; } = [];

        /// <summary>Zusätzliche Adressen für Infos (z. B. Begleitpersonen), eine pro Zeile.</summary>
        public string? InfoEmails { get; set; }

        public bool DatenschutzAkzeptiert { get; set; }
    }

    /// <summary>Geprüfte, bereinigte Anmeldedaten (Adressen normalisiert, ausgeschaltete Felder leer).</summary>
    public sealed record GepruefteAnmeldung(
        string Email,
        string Vorname,
        string Nachname,
        string? Telefon,
        string? Verein,
        string? Graduierung,
        string? Bemerkung,
        int AnzahlBegleitpersonen,
        IReadOnlyList<int> TagIds,
        IReadOnlyList<string> InfoEmails);

    /// <param name="Fehler">Feldname (nameof(AnmeldeEingabe.X)) → Meldung.</param>
    public sealed record AnmeldePruefung(GepruefteAnmeldung? Anmeldung, IReadOnlyDictionary<string, string> Fehler)
    {
        public bool IstGueltig => Anmeldung is not null;
    }

    /// <summary>Prüft eine Anmeldung gegen die Einstellungen der Veranstaltung.</summary>
    public static class AnmeldeValidierung
    {
        private static readonly char[] AdressTrenner = ['\n', '\r', ',', ';', ' '];

        public static AnmeldePruefung Pruefen(Veranstaltung veranstaltung, IReadOnlyCollection<VeranstaltungsTag> tage, AnmeldeEingabe e, int maxInfoEmails)
        {
            var fehler = new Dictionary<string, string>();

            var vorname = Pflicht(e.Vorname, 100, nameof(e.Vorname), "Bitte gib deinen Vornamen an.", fehler);
            var nachname = Pflicht(e.Nachname, 100, nameof(e.Nachname), "Bitte gib deinen Nachnamen an.", fehler);

            string? email = null;
            if (string.IsNullOrWhiteSpace(e.Email))
                fehler[nameof(e.Email)] = Messages.EmailRequiredErrorMessage;
            else if (!EmailAdresse.IstGueltig(e.Email) || e.Email.Trim().Length > 320)
                fehler[nameof(e.Email)] = Messages.EmailFormatErrorMessage;
            else
                email = EmailAdresse.Normalisieren(e.Email);

            var telefon = Optional(e.Telefon, veranstaltung.TelefonFeld, 50, nameof(e.Telefon), "Bitte gib eine Telefonnummer an.", fehler);
            var verein = Optional(e.Verein, veranstaltung.VereinFeld, 200, nameof(e.Verein), "Bitte gib deinen Verein bzw. dein Dojo an.", fehler);
            var graduierung = Optional(e.Graduierung, veranstaltung.GraduierungFeld, 100, nameof(e.Graduierung), "Bitte gib deine Graduierung an.", fehler);
            var bemerkung = Optional(e.Bemerkung, veranstaltung.BemerkungFeld, 2000, nameof(e.Bemerkung), "Bitte fülle das Feld Bemerkung aus.", fehler);

            if (e.AnzahlBegleitpersonen < 0 || e.AnzahlBegleitpersonen > veranstaltung.MaxBegleitpersonen)
                fehler[nameof(e.AnzahlBegleitpersonen)] = veranstaltung.MaxBegleitpersonen == 0
                    ? "Für diese Veranstaltung sind keine Begleitpersonen möglich."
                    : $"Es sind höchstens {veranstaltung.MaxBegleitpersonen} Begleitpersonen möglich.";

            var tagIds = PruefeTage(veranstaltung, tage, e.TagIds, fehler);
            var infoEmails = PruefeInfoEmails(e, email, maxInfoEmails, fehler);

            if (!e.DatenschutzAkzeptiert)
                fehler[nameof(e.DatenschutzAkzeptiert)] = "Bitte stimme der Verarbeitung deiner Daten zu.";

            if (fehler.Count > 0)
                return new AnmeldePruefung(null, fehler);

            return new AnmeldePruefung(
                new GepruefteAnmeldung(email!, vorname!, nachname!, telefon, verein, graduierung, bemerkung, e.AnzahlBegleitpersonen, tagIds, infoEmails),
                fehler);
        }

        private static string? Pflicht(string? wert, int maxLaenge, string feld, string meldung, Dictionary<string, string> fehler)
        {
            var bereinigt = wert?.Trim();
            if (string.IsNullOrEmpty(bereinigt))
                fehler[feld] = meldung;
            else if (bereinigt.Length > maxLaenge)
                fehler[feld] = $"Höchstens {maxLaenge} Zeichen.";
            return bereinigt;
        }

        private static string? Optional(string? wert, FormularFeldModus modus, int maxLaenge, string feld, string pflichtMeldung, Dictionary<string, string> fehler)
        {
            // Ausgeschaltete Felder werden ignoriert, auch wenn jemand sie trotzdem mitschickt
            if (modus == FormularFeldModus.Aus)
                return null;

            var bereinigt = string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
            if (bereinigt is null && modus == FormularFeldModus.Pflicht)
                fehler[feld] = pflichtMeldung;
            else if (bereinigt?.Length > maxLaenge)
                fehler[feld] = $"Höchstens {maxLaenge} Zeichen.";
            return bereinigt;
        }

        private static IReadOnlyList<int> PruefeTage(Veranstaltung veranstaltung, IReadOnlyCollection<VeranstaltungsTag> tage, List<int>? gewaehlt, Dictionary<string, string> fehler)
        {
            // Bei NurGesamt gilt die Anmeldung für alle Tage; es werden keine Tage gespeichert
            if (veranstaltung.Teilnahmemodus == Teilnahmemodus.NurGesamt)
                return [];

            var aktiveTagIds = tage.Where(t => !t.Abgesagt).Select(t => t.Id).ToHashSet();
            // Aus einem Formular gebunden kann die Liste fehlen
            var auswahl = (gewaehlt ?? []).Distinct().ToList();
            var mindestens = Math.Clamp(veranstaltung.MinTageBeiTeilanmeldung, 1, Math.Max(1, aktiveTagIds.Count));

            if (auswahl.Any(id => !aktiveTagIds.Contains(id)))
                fehler[nameof(AnmeldeEingabe.TagIds)] = "Bitte wähle nur angebotene Tage aus.";
            else if (auswahl.Count < mindestens)
                fehler[nameof(AnmeldeEingabe.TagIds)] = mindestens == 1
                    ? "Bitte wähle mindestens einen Tag aus."
                    : $"Bitte wähle mindestens {mindestens} Tage aus.";

            return auswahl.Order().ToList();
        }

        private static IReadOnlyList<string> PruefeInfoEmails(AnmeldeEingabe e, string? eigeneEmail, int maxInfoEmails, Dictionary<string, string> fehler)
        {
            var adressen = (e.InfoEmails ?? string.Empty)
                .Split(AdressTrenner, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (adressen.Count == 0)
                return [];

            var ungueltig = adressen.FirstOrDefault(a => !EmailAdresse.IstGueltig(a));
            if (ungueltig is not null)
            {
                fehler[nameof(e.InfoEmails)] = $"\"{ungueltig}\" ist keine gültige E-Mail-Adresse.";
                return [];
            }

            var normalisiert = adressen.Select(EmailAdresse.Normalisieren).Distinct().Where(a => a != eigeneEmail).ToList();

            // Schutz vor Missbrauch als Spam-Verteiler: nur so viele Adressen wie Begleitpersonen
            var erlaubt = Math.Min(maxInfoEmails, Math.Max(0, e.AnzahlBegleitpersonen));
            if (normalisiert.Count > erlaubt)
                fehler[nameof(e.InfoEmails)] = erlaubt == 0
                    ? "Weitere Adressen sind nur für Begleitpersonen möglich."
                    : $"Höchstens {erlaubt} weitere Adresse(n) – eine je Begleitperson.";

            return normalisiert;
        }
    }
}
