# Webseite für das Budo Shuren Dojo Augsburg

Die Webseite wird mit Blazor .NET 10 und TailwindCSS aufgebaut.

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

- E-Mail Benachrichtigungen einbauen (Sammelemails, Benachrichtigungen zu Events, "Newsletter abonieren" o.ä.)
- Teil für (interne) Termine - wie Seminare usw.
- - Noch besser: Möglichkeit eine Registrierung für Teilnahme zu Events aufzubauen (Event definieren, News Emails, Registrierung, SelfService Verwaltung usw)
- Update durchführen

Allgemeine ToDos:

- Animationen einbauen (Buttons, Umleitungen, Scrollen, Wischen usw) -> sieht hübscher aus + besseres feeling
- mögliche Optimierungen ermitteln (Performance)
- "Wissens"-Beiträge schreiben (verbessert auch SEO)

Letzte Änderungen 20.08.2026

- statische Bilder aktualisiert (Neues Dojo, Eingang, Trainingsfläche)
- Logging erweitert
- "Aktuelles" eingebaut - Blog System, u.a. für SEO Content
- "Wissen"
