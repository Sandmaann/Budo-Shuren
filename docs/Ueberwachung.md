# Überwachung (Health Checks)

Die Website prüft ihren eigenen Zustand. Abrufbar ist das Ergebnis an drei Stellen:

| Wo | Was | Wer |
|---|---|---|
| `GET /health` | Betrieb: Datenbank, Hintergrunddienste, Mail-Warteschlange | Admins, oder mit Token |
| `GET /health/alle` | zusätzlich die Datenprüfungen | Admins, oder mit Token |
| Admin-Seite, Abschnitt „Systemzustand“ | wie `/health/alle`, lesbar aufbereitet | Admins |

Anonym ist nichts erreichbar: Ohne Admin-Login und ohne passenden Token kommt `401`.

## Ergebnis
- **Gesund / eingeschränkt:** HTTP 200
- **Ungesund:** HTTP 503. Dann muss sich jemand kümmern.
- Die Antwort ist JSON mit `status`, `version` und einer Liste `pruefungen` (Name, Status, Beschreibung, Kennzahlen).
- Sie enthält keine E-Mail-Adressen, Fehlertexte oder Verbindungsdaten. Die stehen im Log.

## Die Prüfungen
**Betrieb** (`/health`, schnell, für die regelmäßige Überwachung):

| Prüfung | Ungesund | Eingeschränkt |
|---|---|---|
| Datenbank | keine Verbindung | ausstehende Migrationen |
| Hintergrunddienste | ein Dienst hat sich nicht gemeldet oder hängt (kein Durchlauf in der erwarteten Zeit) | letzter Durchlauf mit Fehler, oder Dienst per Schalter abgeschaltet |
| Mail-Warteschlange | fällige Mails liegen länger als 30 Minuten | in den letzten 7 Tagen endgültig fehlgeschlagene Mails |

Erwartete Abstände der Hintergrunddienste:
- Mailversand: etwa 31 Minuten
- Veranstaltungen: 16 Minuten
- Bilder aufräumen: 2 Stunden

**Daten** (nur `/health/alle` und Admin-Seite, höchstens „eingeschränkt“):
- **Bilder:**
  - nie gespeicherte Uploads, die das Aufräumen längst hätte löschen müssen
  - Bilder ohne Verwendung, die älter als einen Tag sind (z. B. abgebrochene Uploads in Galerie, Neuigkeiten oder Wissen, ersetzte Bilder), mit Anzahl, Größe und IDs
  - Gelöscht wird dabei nichts.
- **Veranstaltungen: Wartung:** liegengebliebene Arbeit der stündlichen Wartung, also abgelaufene unbestätigte Anmeldungen und vergangene Veranstaltungen, die nicht abgeschlossen sind
- **Veranstaltungen: Kalender:** fehlende oder überzählige Kalendereinträge. Erneutes Speichern der Veranstaltung gleicht sie ab.
- **Veranstaltungen: Anmeldungen:** kommende Veranstaltungen mit überbuchtem Termin oder ohne aktiven Empfänger für Benachrichtigungen

Ist das Modul Veranstaltungen ausgeschaltet, werden dessen Prüfungen übersprungen.

## Einrichtung mit BetterStack Uptime
1. **Token setzen:** Auf dem Server in `appsettings.json` einen langen Zufallswert eintragen:
   ```json
   "Systemzustand": { "Token": "<zufälliger Wert>" }
   ```
   Einen Wert erzeugt z. B. PowerShell:
   ```powershell
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
   ```
   Das Deploy-Skript legt den Schlüssel leer an und überschreibt einen gesetzten Wert nicht. Leer heißt: nur Admins haben Zugriff.
2. **Monitor in BetterStack Uptime anlegen:**
   - Typ: „URL becomes unavailable“ (Alarm, wenn der Statuscode nicht 2xx ist)
   - URL: `https://www.budo-shuren-dojo.de/health`
   - Intervall: 3 Minuten
3. **Token mitsenden:** In den erweiterten Einstellungen des Monitors unter den Request-Headern `X-Health-Token` mit dem Wert aus Schritt 1 eintragen.
4. **Testen:**
   - Die URL im Browser ohne Login aufrufen: `401`
   - Der Monitor zeigt „Up“.
   - Während eines Deploys liefert der Wartungsmodus kurz `503`; das ist erwartet.

Der regelmäßige Aufruf hält die App zugleich am Laufen, solange IIS sie nicht im Leerlauf beendet. Unabhängig davon ist für die Hintergrunddienste im IIS empfohlen:
- App-Pool: `startMode=AlwaysRunning`, `idleTimeout=0`
- Website: `preloadEnabled=true`
- Windows-Feature: „Application Initialization“

## Logging
- „Eingeschränkt“ wird nicht bei jedem Aufruf geloggt (`nlog.config`), sonst entstünde bei jedem Monitor-Aufruf eine Warnung.
- „Ungesund“ wird als Fehler geloggt und landet damit auch in BetterStack Logs.
