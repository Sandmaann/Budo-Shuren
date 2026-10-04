# Webseite für das Budo Shuren Dojo Augsburg

Die Webseite wird mit Blazor .NET 10 und TailwindCSS aufgebaut.

## Lizenz

Alle Rechte vorbehalten, siehe [LICENSE](LICENSE). Der Quelltext ist nur zur Ansicht veröffentlicht. Code, Design und Inhalte (Texte, Bilder, Logos) dürfen nur nach vorheriger Rückfrage und schriftlicher Zustimmung verwendet werden.

Die Oberfläche nutzt Komponenten von [Syncfusion](https://www.syncfusion.com/). Sie sind kommerziell lizenziert. Wer das Projekt baut oder betreibt, braucht einen eigenen Lizenzschlüssel (z. B. über die Community License).

## Lokale Einrichtung

Geheimnisse stehen nie im Repository. Lokal kommen sie in die User Secrets, auf dem Server in die dortige `appsettings.json`. Aus `BudoShurenWebsite/BudoShurenWebsite/`:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<verbindung>"   # nur, wenn der Standard aus appsettings.json nicht passt
dotnet user-secrets set "AdminStart:Email" "<email>"                           # erster Admin in einer neuen Datenbank
dotnet user-secrets set "AdminStart:Passwort" "<passwort>"
dotnet user-secrets set "Syncfusion:LicenseKey" "<schlüssel>"
dotnet user-secrets set "BetterStack:SourceToken" "<token>"                    # optional
dotnet user-secrets set "BetterStack:Endpoint" "<https://…betterstackdata.com>"
```

- Ohne `Syncfusion:LicenseKey` läuft die App, die Syncfusion-Komponenten zeigen aber einen Lizenzhinweis.
- Ohne `BetterStack:SourceToken` und `BetterStack:Endpoint` wird nur auf Konsole und in `logs/` geloggt.
- Tests und CI brauchen keinen dieser Werte.

## Branches & Releases

Einfacher GitHub Flow:

- **`main`** ist immer deploybar. Änderungen kommen nur per Pull Request hinein, nie per direktem Push.
- **Arbeits-Branches** werden immer von `main` abgezweigt und sind kurzlebig:
  - `feature/<thema>` – neue Funktionen (z. B. `feature/event-anmeldung`)
  - `fix/<thema>` – Fehlerbehebungen, auch Hotfixes (z. B. `fix/login-cookie`)
  - `chore/<thema>` – Aufräumen, Abhängigkeiten, Doku, Build (z. B. `chore/nuget-updates`)
- **Merge** per *Squash and merge* (ein PR = ein Commit auf `main`, PR-Titel = Commit-Titel). Der Branch wird danach automatisch gelöscht.
- **Releases** sind Tags auf `main`, keine Branches:
  1. Version in `BudoShurenWebsite/BudoShurenWebsite.csproj` (`AssemblyVersion`/`FileVersion`) im PR hochzählen.
  2. Nach dem Merge taggen und pushen: `git tag -a v2.0.0.2 -m "Release 2.0.0.2"` und `git push origin v2.0.0.2`
  3. Genau diesen Tag deployen (`DeployScript.ps1`).
- **Hotfix** für die Live-Version: `fix/…` von `main`, PR, Merge, neuen Tag setzen und deployen.

Lokal nach dem Merge aufräumen: `git switch main`, `git pull` und `git fetch --prune`.

Aktuelle ToDos:

- E-Mail Benachrichtigungen: "Newsletter abonieren" o.ä. (Rundmails und Benachrichtigungen zu Veranstaltungen gibt es seit 2.1)
- Teil für (interne) Termine - wie Seminare usw.
- Update durchführen
- Veranstaltungen Phase 2: Warteliste, Rundmail-Filter, Kalenderdatei (ICS) und Erinnerungen, Druckliste, Bezahlstatus, Duplizieren, Anonymisierung

Allgemeine ToDos:

- Animationen einbauen (Buttons, Umleitungen, Scrollen, Wischen usw) -> sieht hübscher aus + besseres feeling
- mögliche Optimierungen ermitteln (Performance)
- "Wissens"-Beiträge schreiben (verbessert auch SEO)

Version 2.1.0.0 (Oktober 2026): Modul "Veranstaltungen"

- Veranstaltungen (ein- und mehrtägig) anlegen, veröffentlichen, absagen; Eintrag im Kalender
- Anmeldung ohne Benutzerkonto nur per E-Mail (mit Bestätigungslink), Begleitpersonen, Info-Adressen, Anmeldung für einzelne Tage einstellbar
- Selbstverwaltung über einen persönlichen Link: Daten ändern, umbuchen, abmelden
- Übersicht für Organisatoren: Teilnehmerliste mit Änderungen, ablehnen, manuell anmelden, CSV, Rundmails, Tag absagen
- Benachrichtigungen an Organisatoren und weitere Adressen (sofort oder als Tageszusammenfassung)
- Admins verwalten alle Veranstaltungen, Abteilungsleiter die ihrer Abteilung und die des Gesamtvereins
- Eingeschaltet wird das Modul auf dem Server mit `Veranstaltungen:Aktiviert = true` in der `appsettings.json` (Standard: aus)

Letzte Änderungen 20.08.2026

- statische Bilder aktualisiert (Neues Dojo, Eingang, Trainingsfläche)
- Logging erweitert
- "Aktuelles" eingebaut - Blog System, u.a. für SEO Content
- "Wissen"
