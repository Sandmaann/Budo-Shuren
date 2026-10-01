# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Website for the Budo Shuren Dojo Augsburg: Blazor (.NET 10, Interactive Server render mode) + TailwindCSS, SQL Server via EF Core, ASP.NET Identity. UI text, domain names and code comments are German — keep it that way.

`BudoShuren.sln` contains only `BudoShurenWebsite/` (the app). Stopping the app and applying migrations manually is done from the admin page (`/Account/Member/Admin`) via `AdminMaintenanceService`. `_BudoShurenWebsite/` and `BlazorTestApp/` are old/experimental copies, not part of the solution — don't edit them. There are no test projects.

## Commands

Run from `BudoShurenWebsite/BudoShurenWebsite/`:

```sh
dotnet build ../BudoShuren.sln
dotnet run                                # applies pending EF migrations on startup
dotnet ef migrations add <Name>           # dotnet-ef is a local tool (.config/dotnet-tools.json)
```

Tailwind (standalone CLI, not npm; the binary lives in a gitignored `Tailwind/` folder next to the solution):

```sh
./../Tailwind/tailwindcss.exe -i ./Styles/budo-shuren.css -o ./wwwroot/budo-shuren.css --watch
```

`wwwroot/budo-shuren.css` is generated — edit `Styles/budo-shuren.css` or `tailwind.config.js` (custom colors `primary`/`error`, fonts `yuji`/`ptsans`, extra screens like `xs`, `3xl`–`5xl`), then rebuild CSS. New Tailwind classes in `.razor` files only appear after the CLI regenerates the output.

The connection string in `appsettings.json` points to a local named SQL Server instance; startup throws if `DefaultConnection` is missing.

## Architecture

- **Startup (`Program.cs`)**: registers all services, then on every start runs `Database.Migrate()` and seeds the roles from `Global/Roles.cs` (Admin, Abteilungsleiter, Editor, Mitglied, Gast). Culture is forced to `de-DE`. Logging is NLog (`nlog.config`, BetterStack target), not the default providers.
- **Authorization**: Identity with `RequireConfirmedAccount`. Policies: `Aktiviert` (custom `VerifiedUserHandler` — checks `ApplicationUser.Verified`, i.e. admin-approved), `NotGuest`, `AdminOnly`. Member/admin management pages live under `Components/Account/Pages/Member/` (route prefix `/Account/Member/...`); `Ausgemustert/` holds retired Identity pages.
- **Data access**: `ApplicationDbContext` is registered both as a factory and scoped. Syncfusion components (`SfGrid`, `SfSchedule`) get their data through custom `DataAdaptor` subclasses in `Data/*Adaptor.cs`, which do search/sort/paging in memory via `DataOperations`. Separate `DataProtectionKeyContext` persists data-protection keys in the DB.
- **Images** are stored in the database (`DbImage`, `ImageService` with ImageSharp) and rendered as base64 data URIs, not as files in `wwwroot`.
- **Block-based content systems** — two parallel ones with the same shape (Beitrag → ordered Blocks, typed by an enum, rendered by a `*BlockRenderer.razor`, edited with per-type editors in `Shared/*/BlockEditor/`, slug URLs via `SlugService`):
  - **Wissen / "Themen"**: models `WissenKategorie/WissenBeitrag/WissenBlock`, `WissenService`, public routes `/themen`, `/themen/{Slug}`, editor `ThemenVerwalten`/`ThemenBeitragEditor`. `WissenBlockTyp` values 0–5 are legacy (kept for DB compatibility, not selectable in the editor); new layout types start at 10.
  - **Aktuelles** (blog/news): models `AktuellesBeitrag/AktuellesBlock/AktuellesBild`, `IAktuellesService`, routes `/aktuelles`, `/aktuelles/{Slug}`, editor `AktuellesVerwalten`/`AktuellesBearbeiten`. Block types: Markdown (Markdig) and image gallery.
  - Project rule (from Copilot instructions): when fixing bugs in Aktuelles components, use the working equivalent in the Themen/Wissen area as the reference.
- **Maintenance mode**: `MaintenanceMiddleware` (first in the pipeline) returns a 503 page whenever `<ContentRootPath>/maintenance.flag` exists. `DeployScript.ps1` creates/removes this flag, backs up the IIS target, merges `appsettings.json` (only fills missing/empty keys, never overwrites server values), copies the main DLL last and touches `web.config` to recycle IIS. Keep the flag path in sync between the two.
- Syncfusion license key is registered in `Program.cs`; Syncfusion UI strings are localized via `Resources/SfResources.resx` + `SyncfusionLocalizer`.
