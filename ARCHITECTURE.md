# Architecture

XafTVF is a DevExpress XAF learning sandbox for the **Table-Valued Function → Non-Persistent XAF DTO** pattern documented in [TVF_PLAN.md](./TVF_PLAN.md). The skill `.claude/skills/tvf-spike/SKILL.md` drives the implementation checklist.

## Solution layout

Stock XAF 3-tier solution defined by [`XafTVF.slnx`](./XafTVF.slnx).

| Project | TFM | Purpose |
| --- | --- | --- |
| `XafTVF.Module` | `net10.0` | Shared business logic, EF Core entities, `XafTVFEFCoreDbContext`, module-level controllers |
| `XafTVF.Blazor.Server` | `net10.0` | Blazor Server UI host (`https://localhost:5001` / `http://localhost:5000`) |
| `XafTVF.Win` | `net10.0-windows` | WinForms UI host |

Build configurations: `Debug`, `Release`, `EasyTest` (sets `#if EASYTEST` and swaps the connection string to `XafTVFEasyTest`).

## Dependencies

- DevExpress 25.2.* (XAF, Reports, Dashboards, Office, PivotGrid, Validation, Security, Chart, EFCore)
- EF Core 10.0.0 (`Microsoft.EntityFrameworkCore.SqlServer`, `.Proxies`, `.InMemory`, `.Design`)
- Transient `System.Security.Cryptography.Xml` 9.0.0 carries two NU1903 advisories — pulled in by DevExpress and not directly referenced here. Ignore unless DevExpress ships an updated package.

## Database

- Engine: **SQL Server LocalDB** (`(localdb)\mssqllocaldb`).
- Connection string lives in `XafTVF/XafTVF.Blazor.Server/appsettings.json` (`ConnectionStrings:ConnectionString`).
- Initial Catalog: `XafTVF` (Debug/Release) or `XafTVFEasyTest` (EasyTest build).
- Created via the XAF migrator: `dotnet run --project XafTVF\XafTVF.Blazor.Server -- --updateDatabase --forceUpdate --silent`.
- Baseline tables (14): auth/security (`PermissionPolicy*`, `ApplicationUser*`), model diffs (`ModelDifference*`), `ReportDataV2`, `DashboardData`, `HCategories`.

## Security

Integrated XAF security in **non-DC** mode with:

- `ApplicationUser` / `ApplicationUserLoginInfo` (custom subclasses of `PermissionPolicyUser`).
- `PermissionPolicyRole` for role-based permissions.
- Lockout enabled, optimistic locking, deferred deletion (`UseDeferredDeletion`, `UseOptimisticLock` in `OnModelCreating`).

Bootstrap users/roles are created by `XafTVF.Module/DatabaseUpdate/Updater.cs` on first run.

## TVF spike pattern (high level)

The full design is in [TVF_PLAN.md](./TVF_PLAN.md). Two-line summary:

1. EF Core maps a SQL TVF (e.g. `dbo.get_top_customers`) to an `IQueryable<TRow>` via `HasDbFunction` + `ToFunction(...)` against a keyless row type (`CustomerSummaryRow`).
2. A XAF `WindowController` exposes a `PopupWindowShowAction` that gathers parameters into a non-persistent param object, executes the TVF via the EF Core DbContext borrowed from a persistent `ObjectSpace`, then projects the rows into DTOs hosted on a `NonPersistentObjectSpace` and opens them as a `ListView`.

Two object spaces, two object types: keyless EF row stays in EF land; XAF only sees the virtual-property DTO.

## Workflow rules (mirrored from CLAUDE.md)

- Always run `dotnet build XafTVF.slnx` before claiming work is done.
- Never run `dotnet ef migrations add` without asking — a PreToolUse hook blocks it.
- When touching XAF EF Core entities, invoke the `xaf-efcore-entities` skill.
- When touching XAF ViewControllers, invoke the `xaf-viewcontroller-patterns` skill.
- Walk through `/tvf-spike` for the full 10-step implementation checklist.
