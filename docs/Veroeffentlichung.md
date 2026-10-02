# Repository veröffentlichen

Anleitung, um das Repository öffentlich zu machen. Geplant vor dem Release von v2.1, wenn die Bugfixes am Veranstaltungsmodul fertig sind.

Diese Datei enthält bewusst keine Geheimnisse, nur wo sie stehen und wie man sie entfernt. Nach der Veröffentlichung kann sie gelöscht werden.

## Ausgangslage (Prüfung vom 02.10.2026)

### Geheimnisse

| Was | Wo im aktuellen Stand | Wo in der Historie |
|---|---|---|
| Syncfusion-Lizenzschlüssel | `Program.cs`, `RegisterLicense("***ENTFERNT***")` | dazu drei ältere Schlüssel in früheren Commits |
| BetterStack-Token | `nlog.config`, Attribut `sourceToken` | seit Commit „Logging: betterstack als dump eingebaut“ (23.08.2026) |
| SQL-Server-Passwort (Server im internen Netz, Benutzer mit Passwort) | nicht mehr vorhanden | Commit „Release 1“ (02.11.2024), Datei `appsettings - Kopieren.Development.json` |
| Data-Protection-Schlüssel, unverschlüsselt, abgelaufen 2025 | `BudoShurenWebsite/keys/key-….xml` | seit „Release 1“ |

### Weitere Funde

- **Commit-Autoren:** 64 Commits mit der Firmen-E-Mail-Adresse, 2 mit dem Rechnernamen als Adresse.
- **`appsettings.json`:** Verbindung zeigt auf den SQL-Server des Entwicklerrechners.
- **Überflüssige Dateien:** `internal-nlog.txt` (Logdatei), `Properties/ServiceDependencies/…/profile.arm.json` (altes Azure-Profil), `wwwroot/images - Kopie/`, `_BudoShurenWebsite/`, `BlazorTestApp/`.
- **Keine Lizenzdatei.** Ohne Lizenz gilt öffentlicher Code als „alle Rechte vorbehalten“.

### Unkritisch

- Telefonnummern und Adressen aus Impressum und Kontaktseite (stehen ohnehin auf der Website).
- `DeployScript.ps1` enthält nur Platzhalter.
- Der GitHub-Workflow nutzt keine Secrets.
- `UserSecretsId` in der `.csproj` ist nur eine Kennung, kein Geheimnis.

### Warum ein neues Repository

Die Historie lässt sich mit git-filter-repo bereinigen. Im **bestehenden** GitHub-Repository bleiben die alten Commits aber über die Pull-Request-Verweise (`refs/pull/*`) sichtbar. Diese sind schreibgeschützt, und es gibt 40 davon. Das SQL-Passwort steckt im zweiten Commit und damit in der Vorgeschichte jedes Pull Requests.

Entfernen kann diese Verweise nur der GitHub-Support. Er hilft laut [GitHub-Doku](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository) nur, wenn sich das Risiko nicht durch Ändern der Zugangsdaten beseitigen lässt. Das ist hier nicht der Fall.

Deshalb: Historie in einer Kopie bereinigen und in ein **neues** Repository pushen. Die Historie bleibt erhalten, nur ohne Geheimnisse. Das alte Repository bleibt privat.

## Reihenfolge

1. Bugfixes am Veranstaltungsmodul fertigstellen.
2. Teil A: Code bereinigen, per PR nach `release/v2.1`.
3. Teil B: Server vorbereiten.
4. Teil C: Zugangsdaten ändern.
5. `release/v2.1` nach `main` mergen (im alten Repository).
6. Teil D: Historie bereinigen und ins neue Repository pushen.
7. Im neuen Repository taggen (`v2.1.0.0`) und deployen.

## Teil A: Code bereinigen

Eigener Branch von `release/v2.1`, PR nach `release/v2.1`.

### A1 Syncfusion-Schlüssel aus der Konfiguration lesen

- `Program.cs` liest den Schlüssel aus `Syncfusion:LicenseKey` statt aus dem Code.
- Fehlt der Wert, nur eine Warnung loggen. Syncfusion zeigt dann einen Lizenzhinweis, die App läuft aber. Tests und CI brauchen den Schlüssel nicht.
- Lokal setzen:

```sh
dotnet user-secrets set "Syncfusion:LicenseKey" "<schlüssel>"
```

### A2 BetterStack-Token aus der Konfiguration lesen

- In `nlog.config` den Token durch einen Platzhalter ersetzen, z. B. `${environment:BETTERSTACK_SOURCE_TOKEN}`.
- **Prüfen:** NLog lädt `nlog.config` vor dem Host. Ob `sourceToken` beim BetterStack-Target Layout-Platzhalter auflöst, ist nicht sicher. Alternative: Token nach dem Laden in `Program.cs` aus der Konfiguration lesen und am Target setzen.
- Ohne Token darf kein Fehler entstehen. Dann nur Konsole und Datei.

### A3 Verbindung in `appsettings.json` neutral setzen

- Auf einen neutralen Standard wie `Server=localhost\SQLEXPRESS;Database=BudoShurenDev;…` setzen.
- Jeder Entwickler und der Server überschreiben den Wert wie bisher (User Secrets bzw. Server-`appsettings.json`).

### A4 Aufräumen

- Löschen: `BudoShurenWebsite/keys/`, `BudoShurenWebsite/internal-nlog.txt`, `Properties/ServiceDependencies/`, `wwwroot/images - Kopie/`, `_BudoShurenWebsite/`, `BlazorTestApp/`.
- Vorher prüfen, ob `serviceDependencies*.json` noch gebraucht wird (Visual-Studio-Verbindungsdienste).
- `.gitignore` ergänzen: `keys/`, `logs/`, `internal-nlog.txt`.

### A5 Lizenz und README

- Lizenzdatei anlegen, z. B. MIT.
- README: Hinweis, dass Syncfusion kommerziell ist und jeder eine eigene Lizenz braucht (Community License möglich).
- README/CLAUDE.md: lokale Einrichtung mit allen User Secrets (Verbindung, `AdminStart`, `Syncfusion:LicenseKey`, BetterStack optional).

## Teil B: Server vorbereiten

**Vor** dem ersten Deploy nach Teil A, sonst fehlen auf dem Server Lizenz und Logging.

- In die `appsettings.json` auf dem Server eintragen: `Syncfusion:LicenseKey`.
- BetterStack-Token je nach Lösung aus A2 als Umgebungsvariable oder in der Server-`appsettings.json` setzen (neuer Token aus Teil C).
- `DeployScript.ps1` ergänzt nur Schlüssel, die in der `appsettings.json` des Repositorys stehen. Die Geheimnisse stehen dort nicht mehr, also muss man sie auf dem Server von Hand eintragen. Vorhandene Werte auf dem Server bleiben bei jedem Deploy erhalten.

## Teil C: Zugangsdaten ändern

Auch nach der Bereinigung nötig: Jeder, der das Repository geklont hat, hat die alten Werte noch.

- [ ] **BetterStack:** neuen Source-Token erzeugen, alten löschen.
- [ ] **SQL-Server:** Falls Server oder Benutzer aus „Release 1“ noch existieren, Passwort ändern.
- [ ] **Syncfusion:** Schlüssel lassen sich nicht sperren. Im Syncfusion-Konto für die aktuelle Version einen neuen erzeugen ist optional.

## Teil D: Historie bereinigen, neues Repository

### D1 Werkzeug installieren

```sh
pip install git-filter-repo
```

### D2 Frische Kopie anlegen

Nie im Arbeitsordner, immer in einer eigenen Kopie außerhalb:

```sh
git clone --mirror https://github.com/<konto>/<altes-repo>.git BudoShuren-bereinigt.git
cd BudoShuren-bereinigt.git
```

### D3 Ersetzungen festlegen

Datei `ersetzungen.txt` **außerhalb** der Kopie anlegen:

```text
regex:RegisterLicense\("[^"]+"\)==>RegisterLicense("***ENTFERNT***")
regex:sourceToken="***ENTFERNT***"$]+"==>sourceToken="***ENTFERNT***"
regex:(?i)(Password|Pwd)=[^;"]+==>\1=***ENTFERNT***
```

Die Regeln enthalten keine Geheimnisse selbst. Der Platzhalter `${…}` aus A2 bleibt durch `[^"$]` unberührt.

### D4 Autoren vereinheitlichen

Alle Autoren-Adressen der Historie auflisten:

```sh
git log --all --format="%an <%ae>" | sort -u
```

Datei `mailmap.txt` außerhalb der Kopie anlegen. Pro alter Adresse eine Zeile, Name und Zieladresse nach Wunsch. Die echten Adressen stehen bewusst nicht in dieser Datei, weil sie sonst selbst öffentlich würden:

```text
Sandmaann <52655384+Sandmaann@users.noreply.github.com> <alte-firmenadresse>
Sandmaann <52655384+Sandmaann@users.noreply.github.com> <alte-rechneradresse>
```

Vorher die eigene Git-Adresse dauerhaft umstellen:

```sh
git config --global user.email "52655384+Sandmaann@users.noreply.github.com"
```

### D5 Umschreiben

```sh
git filter-repo --sensitive-data-removal \
  --replace-text ../ersetzungen.txt \
  --mailmap ../mailmap.txt \
  --invert-paths \
  --path "BudoShurenWebsite/appsettings - Kopieren.json" \
  --path "BudoShurenWebsite/appsettings - Kopieren.Development.json" \
  --path "BudoShurenWebsite/appsettings_astrum.json" \
  --path "BudoShurenWebsite/appsettings_astrum.Development.json" \
  --path "BudoShurenWebsite/keys/" \
  --path "BudoShurenWebsite/internal-nlog.txt"
```

### D6 Prüfen

Alle Befehle müssen **leer** bleiben:

```sh
git log --all -p | grep -E "RegisterLicense\(\"[^*]"
git log --all -p | grep -E "sourceToken=\"[^*$]"
git log --all -p | grep -iE "(Password|Pwd)=[^*;\"]"
git log --all --format="%ae" | sort -u | grep -v users.noreply.github.com
git log --all --name-only --format= | grep -E "keys/|internal-nlog|Kopieren|astrum"
```

Zusätzlich stichprobenartig die Original-Geheimnisse direkt suchen (z. B. die ersten Zeichen des Tokens mit `git log --all -S"<anfang>"`).

### D7 Neues Repository und Push

- Auf GitHub ein neues, zunächst **privates** Repository ohne README anlegen.
- Nur Branches und Tags pushen, keine Pull-Request-Verweise:

```sh
git remote add neu https://github.com/<konto>/<neues-repo>.git
git push neu --all
git push neu --tags
```

- Kurz prüfen (Dateien, Historie, Actions laufen grün), dann auf öffentlich stellen.

### D8 Einstellungen im neuen Repository

- **Security:** Secret scanning und Push protection einschalten.
- **Branches:** Schutzregel für `main` wie im alten Repository, nur Squash-Merge, Branches automatisch löschen.
- **Actions:** Bei Pull Requests aus Forks Freigabe verlangen („Require approval for all outside collaborators“).

### D9 Lokal umstellen und altes Repository archivieren

- Arbeitsordner auf das neue Repository umstellen. Da sich alle Commit-Kennungen ändern, am einfachsten **neu klonen** statt umzuhängen.
- User Secrets bleiben erhalten, sie hängen an der `UserSecretsId`, nicht am Ordner.
- Altes Repository privat lassen und archivieren.

### Was verloren geht

- Alle Commit-Kennungen ändern sich. Verweise wie `#55` in Commit-Nachrichten zeigen im neuen Repository ins Leere.
- Pull Requests, Issues und Kommentare ziehen nicht mit um.
