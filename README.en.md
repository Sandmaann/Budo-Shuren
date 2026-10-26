# Budo Shuren Dojo Augsburg – Website

[![Tests](https://github.com/Sandmaann/Budo-Shuren/actions/workflows/tests.yml/badge.svg)](https://github.com/Sandmaann/Budo-Shuren/actions/workflows/tests.yml)

🇩🇪 [Deutsche Fassung](README.md)

Source code of the website of the Budo Shuren Dojo Augsburg, a martial arts club in Augsburg, Germany: [www.budo-shuren-dojo.de](https://www.budo-shuren-dojo.de)

The site presents the club and its departments (Aikido, Genbukan, Bujinkan) and also offers an internal area for members and department heads. The website itself, the code comments and the commit history are in German.

> **Note:** The source code is published for viewing only; no open-source license is granted. See [License](#license).

## About the project

I started this project in April 2024 as a learning project for Blazor Server – hence Blazor, even though it is really overkill for a club website. The foundation was built without AI; AI tools (GitHub Copilot, Claude Code) only came in later, during live operation.

## Features

**Public**

- Presentation of the club, its departments, instructors and dojo; training calendar
- **News** (blog) and **Topics** (knowledge articles), both with a block editor
- **Events** with sign-up without a user account (confirmation by e-mail, self-service via a personal link)
- Contact form with spam protection, no captcha
- Galleries, documents (statutes, membership form, grading syllabus)

**Internal**

- Member management with roles (Admin, department head, editor, member, guest) and approval by admins
- Editing of content, images, calendar and events; drafts are saved automatically
- Admin page with system health, contact statistics, maintenance mode and migrations

## Tech stack

| Area | Used |
| --- | --- |
| Frontend / backend | Blazor (.NET 10, Interactive Server), static SSR for public forms |
| Styling | TailwindCSS 3.4 (standalone CLI) |
| UI components | [Syncfusion Blazor](https://www.syncfusion.com/) (grid, scheduler) |
| Database | SQL Server via EF Core, ASP.NET Identity |
| E-mail | MailKit with an outbox queue |
| Logging | NLog, optionally BetterStack |
| Tests | xUnit v3, Shouldly, bUnit, NSubstitute, Testcontainers |
| Hosting | IIS, deployment via PowerShell script |

## Project structure

```
BudoShuren.sln
├── BudoShurenWebsite/          The web application
│   ├── Components/             Razor pages and components
│   ├── Services/               Business logic (mail, contact, events, images, system health …)
│   ├── Models/  Data/          EF models, DbContext, Syncfusion adaptors
│   ├── Migrations/             EF Core migrations
│   ├── Styles/                 Tailwind source (budo-shuren.css)
│   ├── wwwroot/                Static files, generated CSS
│   └── DeployScript.ps1        Deployment to the server
└── BudoShurenWebsite.Tests/    Unit, component and integration tests
```

## Local setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (e.g. SQL Express or LocalDB)
- Tailwind standalone CLI **v3.4.3** (not v4, the config is v3 style)
- Optional: Docker, so the integration tests can start their own database

### 1. Set secrets and connection

Secrets are never stored in the repository. Locally they go into user secrets, on the server into the `appsettings.json` there. From the `BudoShurenWebsite/` folder:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection>"   # only if the default from appsettings.json doesn't fit
dotnet user-secrets set "AdminStart:Email" "<email>"                           # first admin in a new database
dotnet user-secrets set "AdminStart:Passwort" "<password>"
dotnet user-secrets set "Syncfusion:LicenseKey" "<key>"
dotnet user-secrets set "BetterStack:SourceToken" "<token>"                    # optional
dotnet user-secrets set "BetterStack:Endpoint" "<https://…betterstackdata.com>"
```

- The default connection is `localhost\SQLEXPRESS`, database `BudoShurenDB`.
- Without `Syncfusion:LicenseKey` the app runs, but the Syncfusion components show a license notice.
- Without `BetterStack:SourceToken` and `BetterStack:Endpoint`, logging goes only to the console and `logs/`.
- Tests and CI need none of these values.

### 2. Run

```sh
cd BudoShurenWebsite
dotnet run
```

On startup, pending migrations are applied, the roles are created and – if there is no admin yet – the admin from `AdminStart` is created.

### 3. Build CSS

The CLI lives as `Tailwind/tailwindcss.exe` in the (gitignored) folder next to the solution. From `BudoShurenWebsite/`:

```sh
./../Tailwind/tailwindcss.exe -i ./Styles/budo-shuren.css -o ./wwwroot/budo-shuren.css --watch
```

`wwwroot/budo-shuren.css` is generated and not edited by hand. New Tailwind classes in `.razor` files only show up after the CLI has rebuilt the output.

### Migrations

```sh
ASPNETCORE_ENVIRONMENT=Test dotnet ef migrations add <Name>
```

`dotnet-ef` is a local tool (`dotnet tool restore`). `ASPNETCORE_ENVIRONMENT=Test` makes sure nothing is logged to BetterStack in the process.

## Tests

From the repository root:

```sh
dotnet test --solution BudoShuren.sln                                            # all tests
dotnet test --solution BudoShuren.sln --filter-not-trait "Category=Integration"  # fast, no database needed
```

Integration tests need a SQL Server: either via the environment variable `BUDO_TEST_SQL` (the database name must contain "Test", the database is wiped on every run) or automatically via a Docker container. If neither is available, they are skipped.

GitHub Actions runs all tests, including the integration tests, on every pull request and every push to `main`.

## Configuration in production

Some features are switched in the `appsettings.json` on the server:

| Key | Default | Effect |
| --- | --- | --- |
| `Veranstaltungen:Aktiviert` | `false` | Events module on/off (menu, pages, background jobs) |
| `Kontaktformular:AufraeumenAktiviert` | `true` | Deletes sender and text of contact requests after 90 days |
| `Systemzustand:Token` | – | Allows `GET /health` without admin login via the `X-Health-Token` header |

**Maintenance mode:** As long as `maintenance.flag` exists in the application folder, the site returns a 503 maintenance page. `DeployScript.ps1` creates and removes the file automatically during deployment.

## Branches & releases

Simple GitHub Flow:

- **`main`** is always deployable. Changes only get in via pull request, never via direct push.
- **Working branches** always branch off `main` and are short-lived:
  - `feature/<topic>` – new features (e.g. `feature/event-anmeldung`)
  - `fix/<topic>` – bug fixes, including hotfixes (e.g. `fix/login-cookie`)
  - `chore/<topic>` – cleanup, dependencies, docs, build (e.g. `chore/nuget-updates`)
- **Merge** via *Squash and merge* (one PR = one commit on `main`, PR title = commit title). The branch is deleted automatically afterwards.
- **Releases** are tags on `main`, not branches:
  1. Bump the version in `BudoShurenWebsite/BudoShurenWebsite.csproj` (`AssemblyVersion`/`FileVersion`) in the PR and add the entry to [CHANGELOG.md](CHANGELOG.md).
  2. After merging, tag and push: `git tag -a v2.0.0.2 -m "Release 2.0.0.2"` and `git push origin v2.0.0.2`
  3. Deploy exactly that tag (`DeployScript.ps1`).
- **Hotfix** for the live version: `fix/…` off `main`, PR, merge, set a new tag and deploy.

Clean up locally after merging: `git switch main`, `git pull` and `git fetch --prune`.

## Planned

**Next**

- Events phase 2: waiting list, filters for circular mails, calendar file (ICS) and reminders, print list, payment status, duplicating, anonymization
- E-mail notifications: "subscribe to newsletter" or similar (circular mails and event notifications exist since 2.1)
- Section for (internal) dates such as seminars
- Update dependencies

**General**

- Animations (buttons, redirects, scrolling, swiping)
- Identify performance optimizations
- Write more "Topics" articles (also for SEO)

The change history is in [CHANGELOG.md](CHANGELOG.md) (German).

## License

All rights reserved, see [LICENSE](LICENSE). The source code is published for viewing only. Code, design and content (texts, images, logos) may only be used after prior inquiry and written consent.

The UI uses components from [Syncfusion](https://www.syncfusion.com/). They are commercially licensed. Anyone building or running the project needs their own license key (e.g. via the Community License).
