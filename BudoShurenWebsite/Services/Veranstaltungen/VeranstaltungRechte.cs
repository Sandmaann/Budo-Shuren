using BudoShurenWebsite.Global;
using BudoShurenWebsite.Models;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>Der angemeldete Benutzer, soweit für die Rechte im Modul relevant.</summary>
    /// <param name="Abteilung">Wert aus ApplicationUser.Abteilung (je nach Registrierungsweg Id oder Name der Abteilung).</param>
    public sealed record VerwaltungsBenutzer(string UserId, string Anzeigename, bool IstAdmin, bool IstAbteilungsleiter, string? Abteilung)
    {
        /// <summary>Aus dem UserService (nach InitializeAsync); null, wenn niemand angemeldet ist.</summary>
        public static VerwaltungsBenutzer? Aus(UserWithRoles? benutzer)
        {
            if (benutzer?.User is not { } user)
                return null;

            var name = $"{user.Vorname} {user.Name}".Trim();
            return new VerwaltungsBenutzer(
                user.Id,
                string.IsNullOrEmpty(name) ? user.UserName ?? user.Id : name,
                Roles.IsAdmin(benutzer.Roles),
                Roles.IsAbteilungsleiter(benutzer.Roles),
                string.IsNullOrWhiteSpace(user.Abteilung) ? null : user.Abteilung);
        }
    }

    /// <summary>
    /// Wer Veranstaltungen verwalten darf: Admins alle, Abteilungsleiter die ihrer eigenen Abteilung
    /// und die des Gesamtvereins (ohne Abteilung).
    /// </summary>
    public static class VeranstaltungRechte
    {
        public static bool DarfModulNutzen(VerwaltungsBenutzer benutzer) =>
            benutzer.IstAdmin || benutzer.IstAbteilungsleiter;

        public static bool DarfVerwalten(VerwaltungsBenutzer benutzer, string? abteilungId, string? abteilungName)
        {
            if (benutzer.IstAdmin)
                return true;
            if (!benutzer.IstAbteilungsleiter)
                return false;
            if (string.IsNullOrWhiteSpace(abteilungId))
                return true;
            if (string.IsNullOrWhiteSpace(benutzer.Abteilung))
                return false;

            // ApplicationUser.Abteilung enthält bei der Registrierung den Namen, an anderen Stellen die Id
            return string.Equals(benutzer.Abteilung, abteilungId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(benutzer.Abteilung, abteilungName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
