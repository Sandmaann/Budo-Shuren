using BudoShurenWebsite.Data;
using BudoShurenWebsite.Global;
using Microsoft.AspNetCore.Identity;

namespace BudoShurenWebsite.Services
{
    /// <summary>
    /// Zugangsdaten für den ersten Admin, appsettings-Abschnitt "AdminStart".
    /// Nie in appsettings.json eintragen, sondern über User Secrets oder Umgebungsvariablen setzen.
    /// </summary>
    public sealed class AdminStartOptionen
    {
        public const string Abschnitt = "AdminStart";

        public string? Email { get; set; }

        public string? Passwort { get; set; }
    }

    public enum AdminAnlageStatus
    {
        /// <summary>Es gibt schon mindestens einen Admin, nichts geändert.</summary>
        AdminVorhanden,
        /// <summary>Kein Admin vorhanden, aber "AdminStart" ist nicht gesetzt.</summary>
        NichtKonfiguriert,
        /// <summary>Neuer Admin-Benutzer angelegt.</summary>
        Angelegt,
        /// <summary>Benutzer mit der Adresse gab es schon, er wurde zum Admin gemacht (Passwort unverändert).</summary>
        Befoerdert,
        Fehler
    }

    public sealed record AdminAnlageErgebnis(AdminAnlageStatus Status, string Meldung);

    /// <summary>
    /// Legt beim Start einen Admin an, wenn es überhaupt keinen gibt (z. B. bei einer neuen Datenbank).
    /// Setzt voraus, dass die Rollen bereits angelegt sind.
    /// </summary>
    public static class AdminBenutzerAnlage
    {
        public static async Task<AdminAnlageErgebnis> SicherstellenAsync(
            UserManager<ApplicationUser> userManager,
            AdminStartOptionen optionen,
            TimeProvider zeit)
        {
            if ((await userManager.GetUsersInRoleAsync(Roles.Admin)).Count > 0)
                return new(AdminAnlageStatus.AdminVorhanden, "Admin vorhanden, keine Anlage nötig");

            if (string.IsNullOrWhiteSpace(optionen.Email) || string.IsNullOrWhiteSpace(optionen.Passwort))
                return new(AdminAnlageStatus.NichtKonfiguriert,
                    $"Es gibt keinen Admin. Zum Anlegen \"{AdminStartOptionen.Abschnitt}:Email\" und \"{AdminStartOptionen.Abschnitt}:Passwort\" setzen (User Secrets) und neu starten.");

            var email = optionen.Email.Trim();
            var vorhanden = await userManager.FindByEmailAsync(email);
            if (vorhanden is not null)
                return await BefoerdernAsync(userManager, vorhanden, zeit);

            var user = new ApplicationUser
            {
                // Wie bei der Registrierung: Benutzername ist die E-Mail-Adresse
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Vorname = "Admin",
                Verified = true,
                VerifiedAt = zeit.GetLocalNow().DateTime,
                VerifiedBy = "System"
            };

            var anlegen = await userManager.CreateAsync(user, optionen.Passwort);
            if (!anlegen.Succeeded)
                return new(AdminAnlageStatus.Fehler, "Admin konnte nicht angelegt werden: " + Fehlertext(anlegen));

            var rolle = await userManager.AddToRoleAsync(user, Roles.Admin);
            if (!rolle.Succeeded)
            {
                // Keinen halb angelegten Benutzer ohne Rolle zurücklassen
                await userManager.DeleteAsync(user);
                return new(AdminAnlageStatus.Fehler, "Admin-Rolle konnte nicht vergeben werden: " + Fehlertext(rolle));
            }

            return new(AdminAnlageStatus.Angelegt, $"Admin {email} angelegt");
        }

        private static async Task<AdminAnlageErgebnis> BefoerdernAsync(UserManager<ApplicationUser> userManager, ApplicationUser user, TimeProvider zeit)
        {
            user.EmailConfirmed = true;
            if (!user.Verified)
            {
                user.Verified = true;
                user.VerifiedAt = zeit.GetLocalNow().DateTime;
                user.VerifiedBy = "System";
            }

            var speichern = await userManager.UpdateAsync(user);
            if (!speichern.Succeeded)
                return new(AdminAnlageStatus.Fehler, $"Benutzer {user.Email} konnte nicht freigeschaltet werden: " + Fehlertext(speichern));

            var rolle = await userManager.AddToRoleAsync(user, Roles.Admin);
            if (!rolle.Succeeded)
                return new(AdminAnlageStatus.Fehler, "Admin-Rolle konnte nicht vergeben werden: " + Fehlertext(rolle));

            // Wie beim Freischalten neuer Mitglieder: Gast-Rolle entfällt
            if (await userManager.IsInRoleAsync(user, Roles.Gast))
                await userManager.RemoveFromRoleAsync(user, Roles.Gast);

            return new(AdminAnlageStatus.Befoerdert, $"Vorhandener Benutzer {user.Email} zum Admin gemacht, Passwort unverändert");
        }

        // Enthält nur Fehlercodes und -beschreibungen von Identity, nie das Passwort
        private static string Fehlertext(IdentityResult ergebnis) =>
            string.Join("; ", ergebnis.Errors.Select(e => e.Description));
    }
}
