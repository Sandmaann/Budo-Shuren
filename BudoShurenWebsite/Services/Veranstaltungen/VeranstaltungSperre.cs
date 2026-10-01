using BudoShurenWebsite.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BudoShurenWebsite.Services.Veranstaltungen
{
    /// <summary>
    /// Sperre pro Veranstaltung für Platzprüfung und Speichern (sp_getapplock, gilt bis zum Ende der Transaktion).
    /// So können zwei gleichzeitige Anmeldungen nicht beide den letzten Platz bekommen.
    /// </summary>
    public static class VeranstaltungSperre
    {
        private static readonly TimeSpan Wartezeit = TimeSpan.FromSeconds(15);

        /// <summary>Muss innerhalb einer offenen Transaktion aufgerufen werden.</summary>
        /// <exception cref="TimeoutException">Wenn die Sperre nicht rechtzeitig frei wird.</exception>
        public static async Task SetzenAsync(ApplicationDbContext kontext, int veranstaltungId, CancellationToken abbruch)
        {
            if (kontext.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Die Sperre einer Veranstaltung braucht eine offene Transaktion.");

            var ergebnis = new SqlParameter("@ergebnis", SqlDbType.Int) { Direction = ParameterDirection.Output };
            await kontext.Database.ExecuteSqlRawAsync(
                "EXEC @ergebnis = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @wartezeit",
                [ergebnis, new SqlParameter("@resource", $"Veranstaltung:{veranstaltungId}"), new SqlParameter("@wartezeit", (int)Wartezeit.TotalMilliseconds)],
                abbruch);

            // >= 0: Sperre erhalten; < 0: Zeitüberschreitung, Deadlock oder Fehler
            if (ergebnis.Value is not int code || code < 0)
                throw new TimeoutException($"Die Veranstaltung {veranstaltungId} ist gerade gesperrt (sp_getapplock: {ergebnis.Value}).");
        }
    }
}
