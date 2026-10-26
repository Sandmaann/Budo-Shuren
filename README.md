# Budo Shuren Dojo Augsburg – Webseite

[![Tests](https://github.com/Sandmaann/Budo-Shuren/actions/workflows/tests.yml/badge.svg)](https://github.com/Sandmaann/Budo-Shuren/actions/workflows/tests.yml)

🇬🇧 [English version](README.en.md)

Quelltext der Webseite des Budo Shuren Dojo Augsburg: [www.budo-shuren-dojo.de](https://www.budo-shuren-dojo.de)

Die Seite stellt den Verein und seine Abteilungen (Aikido, Genbukan, Bujinkan) vor und bietet daneben einen internen Bereich für Mitglieder und Abteilungsleiter.

> **Hinweis:** Der Quelltext ist nur zur Ansicht veröffentlicht, es gibt keine Open-Source-Lizenz. Siehe [Lizenz](#lizenz).

## Über das Projekt

Das Projekt habe ich im April 2024 als Lernprojekt für Blazor Server begonnen – daher Blazor, auch wenn das für eine Vereinsseite eigentlich überdimensioniert ist. Die Grundlage ist ohne KI entstanden; KI-Werkzeuge (GitHub Copilot, Claude Code) kamen erst später während des Live-Betriebs dazu.

## Funktionen

**Öffentlich**

- Vorstellung von Verein, Abteilungen, Trainern und Dojo; Trainingskalender
- **Aktuelles** (Blog) und **Themen** (Wissensbeiträge), beide mit Block-Editor
- **Veranstaltungen** mit Anmeldung ohne Benutzerkonto (Bestätigung per E-Mail, Selbstverwaltung über persönlichen Link)
- Kontaktformular mit Spam-Schutz ohne Captcha
- Galerien, Dokumente (Satzung, Anmeldeformular, Prüfungsprogramm)

**Intern**

- Mitgliederverwaltung mit Rollen (Admin, Abteilungsleiter, Editor, Mitglied, Gast) und Freigabe durch Admins
- Pflege von Inhalten, Bildern, Kalender und Veranstaltungen; Entwürfe werden automatisch gesichert
- Admin-Seite mit Systemzustand, Kontaktstatistik, Wartungsmodus und Migrationen

## Technik

| Bereich | Eingesetzt |
| --- | --- |
| Frontend / Backend | Blazor (.NET 10, Interactive Server), statisches SSR für öffentliche Formulare |
| Styling | TailwindCSS 3.4 (Standalone-CLI) |
| UI-Komponenten | [Syncfusion Blazor](https://www.syncfusion.com/) (Grid, Scheduler) |
| Datenbank | SQL Server über EF Core, ASP.NET Identity |
| E-Mail | MailKit mit Outbox-Warteschlange |
| Logging | NLog, optional BetterStack |
| Tests | xUnit v3, Shouldly, bUnit, NSubstitute, Testcontainers |
| Hosting | IIS, Deployment per PowerShell-Skript |

## Projektstruktur

```
BudoShuren.sln
├── BudoShurenWebsite/          Die Webanwendung
│   ├── Components/             Razor-Seiten und -Komponenten
│   ├── Services/               Fachlogik (Mail, Kontakt, Veranstaltungen, Bilder, Systemzustand …)
│   ├── Models/  Data/          EF-Modelle, DbContext, Syncfusion-Adaptoren
│   ├── Migrations/             EF-Core-Migrationen
│   ├── Styles/                 Tailwind-Quelle (budo-shuren.css)
│   ├── wwwroot/                Statische Dateien, generiertes CSS
│   └── DeployScript.ps1        Deployment auf den Server
└── BudoShurenWebsite.Tests/    Unit-, Komponenten- und Integrationstests
```

## Lokale Einrichtung

### Voraussetzungen

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (z. B. SQL Express oder LocalDB)
- Tailwind-Standalone-CLI **v3.4.3** (nicht v4, die Konfiguration ist im v3-Stil)
- Optional: Docker, damit die Integrationstests eine eigene Datenbank starten können

### 1. Geheimnisse und Verbindung setzen

Geheimnisse stehen nie im Repository. Lokal kommen sie in die User Secrets, auf dem Server in die dortige `appsettings.json`. Aus dem Ordner `BudoShurenWebsite/`:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<verbindung>"   # nur, wenn der Standard aus appsettings.json nicht passt
dotnet user-secrets set "AdminStart:Email" "<email>"                           # erster Admin in einer neuen Datenbank
dotnet user-secrets set "AdminStart:Passwort" "<passwort>"
dotnet user-secrets set "Syncfusion:LicenseKey" "<schlüssel>"
dotnet user-secrets set "BetterStack:SourceToken" "<token>"                    # optional
dotnet user-secrets set "BetterStack:Endpoint" "<https://…betterstackdata.com>"
```

- Standard-Verbindung ist `localhost\SQLEXPRESS`, Datenbank `BudoShurenDB`.
- Ohne `Syncfusion:LicenseKey` läuft die App, die Syncfusion-Komponenten zeigen aber einen Lizenzhinweis.
- Ohne `BetterStack:SourceToken` und `BetterStack:Endpoint` wird nur auf Konsole und in `logs/` geloggt.
- Tests und CI brauchen keinen dieser Werte.

### 2. Starten

```sh
cd BudoShurenWebsite
dotnet run
```

Beim Start werden ausstehende Migrationen angewendet, die Rollen angelegt und – falls es noch keinen Admin gibt – der Admin aus `AdminStart` erzeugt.

### 3. CSS bauen

Die CLI liegt als `Tailwind/tailwindcss.exe` im (gitignorierten) Ordner neben der Solution. Aus `BudoShurenWebsite/`:

```sh
./../Tailwind/tailwindcss.exe -i ./Styles/budo-shuren.css -o ./wwwroot/budo-shuren.css --watch
```

`wwwroot/budo-shuren.css` wird erzeugt und nicht von Hand bearbeitet. Neue Tailwind-Klassen in `.razor`-Dateien erscheinen erst, nachdem die CLI neu gebaut hat.

### Migrationen

```sh
ASPNETCORE_ENVIRONMENT=Test dotnet ef migrations add <Name>
```

`dotnet-ef` ist ein lokales Tool (`dotnet tool restore`). `ASPNETCORE_ENVIRONMENT=Test` sorgt dafür, dass dabei nichts an BetterStack geloggt wird.

## Tests

Aus dem Repository-Wurzelverzeichnis:

```sh
dotnet test --solution BudoShuren.sln                                            # alle Tests
dotnet test --solution BudoShuren.sln --filter-not-trait "Category=Integration"  # schnell, ohne Datenbank
```

Integrationstests brauchen einen SQL Server: entweder über die Umgebungsvariable `BUDO_TEST_SQL` (der Datenbankname muss „Test“ enthalten, die Datenbank wird bei jedem Lauf geleert) oder automatisch per Docker-Container. Ist beides nicht vorhanden, werden sie übersprungen.

GitHub Actions führt bei jedem Pull Request und Push auf `main` alle Tests aus, auch die Integrationstests.

## Konfiguration im Betrieb

Einige Funktionen werden in der `appsettings.json` auf dem Server geschaltet:

| Schlüssel | Standard | Wirkung |
| --- | --- | --- |
| `Veranstaltungen:Aktiviert` | `false` | Modul „Veranstaltungen“ ein/aus (Menü, Seiten, Hintergrundjobs) |
| `Kontaktformular:AufraeumenAktiviert` | `true` | Löscht Absender und Text von Kontaktanfragen nach 90 Tagen |
| `Systemzustand:Token` | – | Erlaubt `GET /health` ohne Admin-Login per Header `X-Health-Token` |

**Wartungsmodus:** Solange `maintenance.flag` im Anwendungsordner liegt, liefert die Seite eine 503-Wartungsseite. `DeployScript.ps1` setzt und entfernt die Datei beim Deployment automatisch.

## Branches & Releases

Einfacher GitHub Flow:

- **`main`** ist immer deploybar. Änderungen kommen nur per Pull Request hinein, nie per direktem Push.
- **Arbeits-Branches** werden immer von `main` abgezweigt und sind kurzlebig:
  - `feature/<thema>` – neue Funktionen (z. B. `feature/event-anmeldung`)
  - `fix/<thema>` – Fehlerbehebungen, auch Hotfixes (z. B. `fix/login-cookie`)
  - `chore/<thema>` – Aufräumen, Abhängigkeiten, Doku, Build (z. B. `chore/nuget-updates`)
- **Merge** per *Squash and merge* (ein PR = ein Commit auf `main`, PR-Titel = Commit-Titel). Der Branch wird danach automatisch gelöscht.
- **Releases** sind Tags auf `main`, keine Branches:
  1. Version in `BudoShurenWebsite/BudoShurenWebsite.csproj` (`AssemblyVersion`/`FileVersion`) im PR hochzählen und den Eintrag in [CHANGELOG.md](CHANGELOG.md) ergänzen.
  2. Nach dem Merge taggen und pushen: `git tag -a v2.0.0.2 -m "Release 2.0.0.2"` und `git push origin v2.0.0.2`
  3. Genau diesen Tag deployen (`DeployScript.ps1`).
- **Hotfix** für die Live-Version: `fix/…` von `main`, PR, Merge, neuen Tag setzen und deployen.

Lokal nach dem Merge aufräumen: `git switch main`, `git pull` und `git fetch --prune`.

## Geplant

**Als Nächstes**

- Veranstaltungen Phase 2: Warteliste, Rundmail-Filter, Kalenderdatei (ICS) und Erinnerungen, Druckliste, Bezahlstatus, Duplizieren, Anonymisierung
- E-Mail-Benachrichtigungen: „Newsletter abonnieren“ o. Ä. (Rundmails und Benachrichtigungen zu Veranstaltungen gibt es seit 2.1)
- Bereich für (interne) Termine wie Seminare
- Abhängigkeiten aktualisieren

**Allgemein**

- Animationen (Buttons, Weiterleitungen, Scrollen, Wischen)
- Performance-Optimierungen ermitteln
- Weitere „Themen“-Beiträge schreiben (auch für SEO)

Was sich bisher geändert hat, steht in [CHANGELOG.md](CHANGELOG.md).

## Lizenz

Alle Rechte vorbehalten, siehe [LICENSE](LICENSE). Der Quelltext ist nur zur Ansicht veröffentlicht. Code, Design und Inhalte (Texte, Bilder, Logos) dürfen nur nach vorheriger Rückfrage und schriftlicher Zustimmung verwendet werden.

Die Oberfläche nutzt Komponenten von [Syncfusion](https://www.syncfusion.com/). Sie sind kommerziell lizenziert. Wer das Projekt baut oder betreibt, braucht einen eigenen Lizenzschlüssel (z. B. über die Community License).
