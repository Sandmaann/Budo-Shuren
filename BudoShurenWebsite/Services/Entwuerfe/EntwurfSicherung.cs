namespace BudoShurenWebsite.Services.Entwuerfe
{
    /// <summary>
    /// Sichert den ungespeicherten Stand einer Bearbeitungsseite laufend als Entwurf (IEntwurfSpeicher).
    /// Blazor Server hält die Eingaben nur in der Verbindung zum Browser. Geht sie verloren (Tab-Wechsel auf dem Handy,
    /// Browser verwirft die Seite, Neustart der App), lädt die Seite neu und stellt den Entwurf wieder her.
    /// Ablauf in der Seite:
    /// <list type="number">
    /// <item>Datensatz laden, dann <see cref="LadenAsync"/>: gibt es einen Entwurf, ersetzt er den geladenen Stand.</item>
    /// <item><see cref="AusgangMerken"/> und <see cref="Starten"/> (nur interaktiv, nicht beim Prerendering).</item>
    /// <item>Nach dem Speichern oder Verwerfen <see cref="LoeschenAsync"/>, neu laden, wieder <see cref="AusgangMerken"/>.</item>
    /// </list>
    /// </summary>
    /// <typeparam name="T">Formularmodell der Seite; muss sich als JSON schreiben und wieder lesen lassen.</typeparam>
    public sealed class EntwurfSicherung<T> : IAsyncDisposable where T : class
    {
        /// <summary>So oft wird geprüft, ob sich der Stand geändert hat.</summary>
        public static readonly TimeSpan Abstand = TimeSpan.FromSeconds(2);

        private readonly IEntwurfSpeicher _speicher;
        private readonly ILogger _logger;
        private readonly Func<T?> _stand;
        private readonly Func<T, IEnumerable<int>> _bildIds;
        private readonly CancellationTokenSource _ende = new();
        // Sichern und Löschen nacheinander: sonst könnte eine laufende Sicherung den gerade gelöschten Entwurf wieder anlegen
        private readonly SemaphoreSlim _sperre = new(1, 1);
        private Task? _schleife;
        private string? _gesichert;
        private bool _angehalten;
        private bool _fehlerGemeldet;

        public string BenutzerId { get; }

        public string Schluessel { get; }

        /// <param name="stand">Liefert den aktuellen Stand des Formulars (null = noch nichts geladen).</param>
        /// <param name="bildIds">Bilder, auf die der Stand verweist (siehe IEntwurfSpeicher.SpeichernAsync).</param>
        public EntwurfSicherung(IEntwurfSpeicher speicher, ILogger logger, string benutzerId, string schluessel, Func<T?> stand, Func<T, IEnumerable<int>>? bildIds = null)
        {
            _speicher = speicher;
            _logger = logger;
            BenutzerId = benutzerId;
            Schluessel = schluessel;
            _stand = stand;
            _bildIds = bildIds ?? (_ => []);
        }

        /// <summary>
        /// Liefert den Entwurf, falls es einen gibt. Hält das Sichern bis zum nächsten <see cref="AusgangMerken"/> an:
        /// der frisch geladene Stand der Seite soll nicht selbst als Entwurf entstehen.
        /// </summary>
        public Task<GeladenerEntwurf<T>?> LadenAsync(CancellationToken abbruch = default)
        {
            _angehalten = true;
            return _speicher.LadenAsync<T>(BenutzerId, Schluessel, abbruch);
        }

        /// <summary>
        /// Der aktuelle Stand gilt als unverändert: gesichert wird erst, wenn er davon abweicht.
        /// Direkt nach dem Laden aufrufen, ohne await dazwischen.
        /// </summary>
        public void AusgangMerken()
        {
            _gesichert = _stand() is { } stand ? EntwurfSpeicher.AlsJson(stand) : null;
            _angehalten = false;
        }

        /// <summary>Beginnt, regelmäßig zu sichern. Mehrfacher Aufruf schadet nicht.</summary>
        /// <param name="imKreislauf">InvokeAsync der Komponente: der Stand wird nur gelesen, während kein anderes Ereignis ihn ändert.</param>
        public void Starten(Func<Func<Task>, Task> imKreislauf) =>
            _schleife ??= Task.Run(() => SchleifeAsync(imKreislauf));

        private async Task SchleifeAsync(Func<Func<Task>, Task> imKreislauf)
        {
            using var takt = new PeriodicTimer(Abstand);
            try
            {
                while (await takt.WaitForNextTickAsync(_ende.Token))
                {
                    try
                    {
                        await imKreislauf(SichernAsync);
                        _fehlerGemeldet = false;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Nur einmal je Störung melden, nicht alle zwei Sekunden
                        if (!_fehlerGemeldet)
                            _logger.LogWarning(ex, "Entwurf {Schluessel} konnte nicht gesichert werden.", Schluessel);
                        _fehlerGemeldet = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>Sichert den Stand, falls er sich seit der letzten Sicherung (oder dem Ausgang) geändert hat.</summary>
        public async Task SichernAsync()
        {
            if (_angehalten || _stand() is not { } stand)
                return;

            // Vor dem ersten await festhalten: danach darf sich der Stand wieder ändern
            var json = EntwurfSpeicher.AlsJson(stand);
            if (json == _gesichert)
                return;
            var bildIds = _bildIds(stand).Distinct().ToList();

            await _sperre.WaitAsync(_ende.Token);
            try
            {
                if (_angehalten)
                    return;
                await _speicher.SpeichernAsync(BenutzerId, Schluessel, json, bildIds, _ende.Token);
                _gesichert = json;
            }
            finally
            {
                _sperre.Release();
            }
        }

        /// <summary>
        /// Löscht den Entwurf (nach dem Speichern oder Verwerfen). Bis zum nächsten <see cref="AusgangMerken"/> wird nicht gesichert,
        /// damit der Stand vor dem Neuladen nicht gleich wieder als Entwurf entsteht.
        /// </summary>
        public async Task LoeschenAsync()
        {
            _angehalten = true;
            await _sperre.WaitAsync(_ende.Token);
            try
            {
                await _speicher.LoeschenAsync(BenutzerId, Schluessel, _ende.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Das eigentliche Speichern ist schon gelungen und soll daran nicht scheitern. Der Entwurf läuft von selbst ab.
                _logger.LogWarning(ex, "Entwurf {Schluessel} konnte nicht gelöscht werden.", Schluessel);
            }
            finally
            {
                _sperre.Release();
            }
        }

        /// <summary>Beendet das Sichern. Der Entwurf bleibt stehen: er wird gebraucht, wenn die Seite neu lädt.</summary>
        public async ValueTask DisposeAsync()
        {
            // Nicht auf die Schleife warten: sie kann gerade auf den Kreislauf warten, der selbst beendet wird
            if (!_ende.IsCancellationRequested)
                await _ende.CancelAsync();
        }
    }
}
