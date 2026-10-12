using BudoShurenWebsite.Data;
using BudoShurenWebsite.Models;
using BudoShurenWebsite.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace BudoShurenWebsite.Services.Kontakt
{
    /// <summary>
    /// Schreibt und liest das Protokoll des Kontaktformulars (<see cref="KontaktAnfrage"/>): wer wann was an welche
    /// Abteilung geschickt hat und was daraus wurde. Die Auswertung steht im Adminbereich (KontaktStatistik).
    /// </summary>
    public sealed class KontaktProtokoll
    {
        /// <summary>Danach werden Absender und Nachricht geleert; Zeitpunkt, Abteilung und Ergebnis bleiben für die Zahlen.</summary>
        public static readonly TimeSpan AufbewahrenFuer = TimeSpan.FromDays(90);

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _zeit;
        private readonly ILogger<KontaktProtokoll> _logger;

        public KontaktProtokoll(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider zeit, ILogger<KontaktProtokoll> logger)
        {
            _dbFactory = dbFactory;
            _zeit = zeit;
            _logger = logger;
        }

        /// <summary>
        /// Trägt eine Nachricht ein (der Zeitpunkt wird hier gesetzt) und leert bei der Gelegenheit abgelaufene Einträge.
        /// Wirft nie: ein Fehler im Protokoll darf das Formular nicht stören.
        /// </summary>
        public async Task EintragenAsync(KontaktAnfrage anfrage)
        {
            // Ohne Adresse und Text: die Logdateien gehen auch an einen externen Dienst
            _logger.LogInformation("Kontaktformular: {Status} (Abteilung {Abteilung}, {Mails} Mails, Seite {Seite}, {Hinweis})",
                anfrage.Status, anfrage.Abteilung, anfrage.AnzahlMails, anfrage.Seite, anfrage.Hinweis);

            try
            {
                var jetzt = _zeit.GetUtcNow().UtcDateTime;
                anfrage.ZeitpunktUtc = jetzt;
                anfrage.Abteilung = Gekuerzt(anfrage.Abteilung, 200);
                anfrage.AbsenderEmail = Gekuerzt(anfrage.AbsenderEmail, 320);
                anfrage.Nachricht = Gekuerzt(anfrage.Nachricht, KontaktAnfrage.MaxNachricht);
                anfrage.Hinweis = Gekuerzt(anfrage.Hinweis, 500);
                anfrage.Seite = Gekuerzt(anfrage.Seite, 200);

                await using var kontext = await _dbFactory.CreateDbContextAsync();
                kontext.KontaktAnfragen.Add(anfrage);
                await kontext.SaveChangesAsync();

                var grenze = jetzt - AufbewahrenFuer;
                await kontext.KontaktAnfragen
                    .Where(a => a.ZeitpunktUtc < grenze && (a.AbsenderEmail != null || a.Nachricht != null))
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(a => a.AbsenderEmail, (string?)null)
                        .SetProperty(a => a.Nachricht, (string?)null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Protokollieren einer Nachricht aus dem Kontaktformular");
            }
        }

        /// <summary>Zahlen je Abteilung und Ablehnungen je Grund für alle Nachrichten seit <paramref name="abUtc"/>.</summary>
        public async Task<KontaktAuswertung> AuswertenAsync(DateTime abUtc, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var gruppen = await kontext.KontaktAnfragen
                .Where(a => a.ZeitpunktUtc >= abUtc)
                .GroupBy(a => new { a.Abteilung, a.Status })
                .Select(g => new { g.Key.Abteilung, g.Key.Status, Anzahl = g.Count(), Mails = g.Sum(a => a.AnzahlMails) })
                .ToListAsync(abbruch);

            var abteilungen = gruppen
                .GroupBy(g => g.Abteilung)
                .Select(g => new KontaktAbteilungZahlen(
                    g.Key,
                    Zugestellt: g.Where(x => x.Status == KontaktAnfrageStatus.Zugestellt).Sum(x => x.Anzahl),
                    Mails: g.Sum(x => x.Mails),
                    VersandFehler: g.Where(x => x.Status == KontaktAnfrageStatus.VersandFehler).Sum(x => x.Anzahl),
                    Abgelehnt: g.Where(x => x.Status.IstAbgelehnt()).Sum(x => x.Anzahl)))
                .OrderBy(z => z.Abteilung)
                .ToList();

            var ablehnungen = gruppen
                .Where(g => g.Status.IstAbgelehnt())
                .GroupBy(g => g.Status)
                .Select(g => new KontaktAblehnungZahl(g.Key, g.Sum(x => x.Anzahl)))
                .OrderBy(z => z.Grund)
                .ToList();

            return new KontaktAuswertung(abteilungen, ablehnungen);
        }

        /// <summary>Die neuesten Einträge, neueste zuerst.</summary>
        /// <param name="mitAbgelehnten">false: nur Nachrichten, die angenommen wurden (zugestellt oder Versandfehler).</param>
        public async Task<List<KontaktAnfrage>> LetzteAsync(int anzahl, bool mitAbgelehnten, CancellationToken abbruch = default)
        {
            await using var kontext = await _dbFactory.CreateDbContextAsync(abbruch);
            var anfragen = kontext.KontaktAnfragen.AsNoTracking();
            if (!mitAbgelehnten)
                anfragen = anfragen.Where(a => a.Status < KontaktAnfrageStatus.Honeypot);

            return await anfragen
                .OrderByDescending(a => a.ZeitpunktUtc)
                .ThenByDescending(a => a.Id)
                .Take(anzahl)
                .ToListAsync(abbruch);
        }

        private static string? Gekuerzt(string? text, int laenge) =>
            text is not null && text.Length > laenge ? text[..laenge] : text;
    }

    /// <param name="Abteilungen">Eine Zeile je Abteilung, die im Zeitraum vorkommt.</param>
    /// <param name="Ablehnungen">Anzahl der Ablehnungen je Grund, über alle Abteilungen.</param>
    public sealed record KontaktAuswertung(IReadOnlyList<KontaktAbteilungZahlen> Abteilungen, IReadOnlyList<KontaktAblehnungZahl> Ablehnungen);

    /// <param name="Abteilung">null bei Einträgen ohne Abteilung.</param>
    /// <param name="Zugestellt">Nachrichten, die bei der Abteilung angekommen sind.</param>
    /// <param name="Mails">Mails, die dafür an die Abteilung und ihre Abteilungsleiter gingen.</param>
    /// <param name="VersandFehler">Angenommene Nachrichten, für die keine Mail rausging.</param>
    /// <param name="Abgelehnt">Als Spam abgelehnte Nachrichten.</param>
    public sealed record KontaktAbteilungZahlen(string? Abteilung, int Zugestellt, int Mails, int VersandFehler, int Abgelehnt);

    public sealed record KontaktAblehnungZahl(KontaktAnfrageStatus Grund, int Anzahl);
}
