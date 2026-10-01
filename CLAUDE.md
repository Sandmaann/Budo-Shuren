# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Website for the Budo Shuren Dojo Augsburg: Blazor (.NET 10, Interactive Server render mode) + TailwindCSS, SQL Server via EF Core, ASP.NET Identity. UI text, domain names and code comments are German — keep it that way.

`BudoShuren.sln` contains `BudoShurenWebsite/` (the app) and `BudoShurenWebsite.Tests/` (tests, see below). Stopping the app and applying migrations manually is done from the admin page (`/Account/Member/Admin`) via `AdminMaintenanceService`. `_BudoShurenWebsite/` and `BlazorTestApp/` are old/experimental copies, not part of the solution — don't edit them.

## Git workflow

GitHub Flow, see "Branches & Releases" in `README.md`: `main` is the only long-lived branch. Work happens on `feature/…`, `fix/…` or `chore/…` branches off `main`, merged via PR (squash only; branches auto-delete). Releases are annotated tags `vX.Y.Z.W` matching `AssemblyVersion` in the `.csproj` — no `develop`/`release` branches. Commit messages and PR texts are German.

## Commands

Run from `BudoShurenWebsite/BudoShurenWebsite/`:

```sh
dotnet build ../BudoShuren.sln
dotnet run                                # applies pending EF migrations on startup
dotnet ef migrations add <Name>           # dotnet-ef is a local tool (.config/dotnet-tools.json)
```

`dotnet ef` runs `Program.Main` at design time. Prefix it with `ASPNETCORE_ENVIRONMENT=Test` so NLog loads the test config and nothing is sent to BetterStack. Keep `dotnet-ef` and the explicit `Microsoft.EntityFrameworkCore.Design` reference on the same version as EF Core.

Tailwind v3.4.3 (standalone CLI, not npm; `tailwindcss-windows-x64.exe` from the GitHub release, saved as `Tailwind/tailwindcss.exe` in the gitignored folder next to the solution — don't use v4, the config is v3 style):

```sh
./../Tailwind/tailwindcss.exe -i ./Styles/budo-shuren.css -o ./wwwroot/budo-shuren.css --watch
```

`wwwroot/budo-shuren.css` is generated — edit `Styles/budo-shuren.css` or `tailwind.config.js` (custom colors `primary`/`error`, fonts `yuji`/`ptsans`, extra screens like `xs`, `3xl`–`5xl`), then rebuild CSS. New Tailwind classes in `.razor` files only appear after the CLI regenerates the output.

The connection string in `appsettings.json` points to a local named SQL Server instance; startup throws if `DefaultConnection` is missing. On a machine without that instance, use a local SQL Server (e.g. SQL Express or LocalDB) and override the connection string via user secrets (never edit `appsettings.json` for this):

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\SQLEXPRESS;Database=BudoShurenDev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

## Tests

`BudoShurenWebsite.Tests/` uses xUnit v3 on Microsoft.Testing.Platform (opted in via `global.json`), Shouldly for assertions, `WebApplicationFactory` for HTTP tests, Testcontainers/Respawn for the database. Run from the repository root:

```sh
dotnet test --solution BudoShuren.sln                                            # all tests
dotnet test --solution BudoShuren.sln --filter-not-trait "Category=Integration"  # fast, no database needed
```

- Categories via `[Trait("Category", …)]`: `Unit` (no DB), `Integration` (real SQL Server, incl. HTTP tests; `Infrastruktur/TestAnmeldung` logs in via the `X-Test-Benutzer` header), `Komponente` (bUnit, services faked with NSubstitute).
- Database tests derive from `Infrastruktur/DatenbankTest` and use `[DatenbankFact]`. The DB comes from env var `BUDO_TEST_SQL`, e.g. `Server=localhost\SQLEXPRESS;Database=BudoShurenTests;Trusted_Connection=True;TrustServerCertificate=True` (the database name must contain "Test" because Respawn wipes it; never point it at `master` or a real DB) or else from a SQL Server container if Docker is available. Without either, these tests are skipped, not failed.
- No SQLite/InMemory: locking (`sp_getapplock`), `rowversion`, filtered indexes and collation must behave like production.
- With `ASPNETCORE_ENVIRONMENT=Test`, `Program.cs` configures NLog in code (warnings to the console) instead of loading `nlog.config`, so tests and `dotnet ef` never log to BetterStack. Don't switch this to a config file: if the file is missing, NLog silently falls back to `nlog.config`.
- GitHub Actions (`.github/workflows/tests.yml`) runs all tests including integration tests on every PR and push to `main`.
- `ModellTests` fails if the EF model changed without a migration.
- `DataProtectionKeyContext` has no migrations; the test fixture creates the `DataProtectionKeys` table itself.

## Architecture

- **Startup (`Program.cs`)**: registers all services, then on every start runs `Database.Migrate()` and seeds the roles from `Global/Roles.cs` (Admin, Abteilungsleiter, Editor, Mitglied, Gast). Culture is forced to `de-DE`. Logging is NLog (`nlog.config`, BetterStack target), not the default providers.
- **Authorization**: Identity with `RequireConfirmedAccount`. Policies: `Aktiviert` (custom `VerifiedUserHandler` — checks `ApplicationUser.Verified`, i.e. admin-approved), `NotGuest`, `AdminOnly`. Member/admin management pages live under `Components/Account/Pages/Member/` (route prefix `/Account/Member/...`); `Ausgemustert/` holds retired Identity pages.
- **Data access**: `ApplicationDbContext` is registered both as a factory and scoped. Syncfusion components (`SfGrid`, `SfSchedule`) get their data through custom `DataAdaptor` subclasses in `Data/*Adaptor.cs`, which do search/sort/paging in memory via `DataOperations`. Separate `DataProtectionKeyContext` persists data-protection keys in the DB.
- **Mail** (`Services/Mail/`): all SMTP traffic goes through `IMailTransport` (`MailKitTransport`, credentials from the `EmailSettings` row with `IsMain`; sender is always that system address). Two ways to send:
  - `EmailSender` sends directly (Identity mails, contact form).
  - `IEmailWarteschlange` is an outbox for new features. `Hinzufuegen` adds an `EmailAusgang` row to the caller's DbContext, so the mail is only sent if the caller's `SaveChanges` succeeds. Call `VersandAnstossen()` after saving.
  - `EmailVersandHostedService` (thin shell around `EmailVersandJob`) sends due mails in batches over one connection, retries with backoff, and deletes old rows.
  - Options are in the appsettings section `EmailVersand` (all optional, validated on start). `Aktiviert=false` in tests. Use `TimeProvider` for time, never `DateTime.Now`, in new services.
- **Veranstaltungen** (events with sign-up; module in progress, see `Models/Veranstaltungen/`, `Services/Veranstaltungen/`):
  - Feature switch `Veranstaltungen:Aktiviert` in appsettings (default `false`; `true` in Development). It hides the menu entry and pages.
  - Rules without DB access live in static classes (`KapazitaetsRechner`, `AnmeldungStatusUebergaenge`, `VeranstaltungAenderungsRegeln`, `VeroeffentlichungsPruefung`, `VeranstaltungRechte`, `KalenderEintragFabrik`). Services use them and check rights themselves: Admins manage everything, Abteilungsleiter only their own Abteilung. `ApplicationUser.Abteilung` may hold the Abteilung's Id or Name.
  - Published public events write one `AppointmentData` per day, linked via `VeranstaltungsTagId`. These entries are read-only in the calendar, and `AppointmentAdaptorComponent` rejects edits to them.
  - Times: domain dates and deadlines are local time (Europe/Berlin, see `Global/Ortszeit`); technical timestamps end in `Utc`.
  - Public pages (`Components/Pages/Veranstaltungen/`) are static SSR, so don't add `@rendermode` to them:
    - Forms post via `[SupplyParameterFromForm]` and antiforgery.
    - `[EnableRateLimiting(VeranstaltungRateLimit.Formulare)]` limits POSTs per IP.
    - `FormularSchutz` provides a honeypot plus a minimum fill time.
  - Links in mails carry a token. Only its SHA-256 hash is stored (`AnmeldeToken`).
    - Opening a link (GET) must never change anything, because mail scanners open them. Actions run only on a POST from a button on the page.
    - Token pages call `VeranstaltungLinks.SicherheitsHeaderSetzen`. Logged paths go through `VeranstaltungLinks.OhneToken`.
  - Render user-facing Markdown with `MarkdownText.SicherZuHtml` (raw HTML disabled), and HTML-encode values in mail templates.
  - Background jobs: `VeranstaltungJobsHostedService` runs `BenachrichtigungJob` every minute and `VeranstaltungWartungJob` hourly. It only runs when `Veranstaltungen:Aktiviert` and `Veranstaltungen:HintergrundJobsAktiviert` (default `true`, `false` in tests) are both on.
    - Organizer notifications: `BenachrichtigungsAuswertung` (no DB) decides per recipient. The job sends events after `BenachrichtigtBisUtc`, either 5 minutes after the last change or as a daily digest.
    - Notification mails go to external addresses too, so they contain no phone, remark or note (`EreignisText.FuerBenachrichtigung`).
    - Links in mails created without a request use `Veranstaltungen:WebsiteUrl` (default production URL; Development: localhost).
- **Images** are stored in the database (`DbImage`, `ImageService` with ImageSharp) and rendered as base64 data URIs, not as files in `wwwroot`.
- **Block-based content systems** — two parallel ones with the same shape (Beitrag → ordered Blocks, typed by an enum, rendered by a `*BlockRenderer.razor`, edited with per-type editors in `Shared/*/BlockEditor/`, slug URLs via `SlugService`):
  - **Wissen / "Themen"**: models `WissenKategorie/WissenBeitrag/WissenBlock`, `WissenService`, public routes `/themen`, `/themen/{Slug}`, editor `ThemenVerwalten`/`ThemenBeitragEditor`. `WissenBlockTyp` values 0–5 are legacy (kept for DB compatibility, not selectable in the editor); new layout types start at 10.
  - **Aktuelles** (blog/news): models `AktuellesBeitrag/AktuellesBlock/AktuellesBild`, `IAktuellesService`, routes `/aktuelles`, `/aktuelles/{Slug}`, editor `AktuellesVerwalten`/`AktuellesBearbeiten`. Block types: Markdown (Markdig) and image gallery.
  - Project rule (from Copilot instructions): when fixing bugs in Aktuelles components, use the working equivalent in the Themen/Wissen area as the reference.
- **Maintenance mode**: `MaintenanceMiddleware` (first in the pipeline) returns a 503 page whenever `<ContentRootPath>/maintenance.flag` exists. `DeployScript.ps1` creates/removes this flag, backs up the IIS target, merges `appsettings.json` (only fills missing/empty keys, never overwrites server values), copies the main DLL last and touches `web.config` to recycle IIS. Keep the flag path in sync between the two.
- Syncfusion license key is registered in `Program.cs`; Syncfusion UI strings are localized via `Resources/SfResources.resx` + `SyncfusionLocalizer`.
