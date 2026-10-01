using BudoShurenWebsite.Models.Enums;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Welche Felder das Anmelde- bzw. Änderungsformular einer Veranstaltung zeigt.</summary>
    public sealed record AnmeldeFormularEinstellungen(
        Teilnahmemodus Teilnahmemodus,
        int MinTageBeiTeilanmeldung,
        int MaxBegleitpersonen,
        FormularFeldModus TelefonFeld,
        FormularFeldModus VereinFeld,
        FormularFeldModus GraduierungFeld,
        FormularFeldModus BemerkungFeld);
}
