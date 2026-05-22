# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project shape

Stock DevExpress XAF 3-tier solution (`XafTVF.slnx`) — a **learning sandbox** for the TVF → NonPersistentObject pattern described in `@TVF_PLAN.md`.

- `XafTVF.Module` — shared business logic, EF Core entities, DbContext (`XafTVFEFCoreDbContext`)
- `XafTVF.Blazor.Server` — Blazor Server UI
- `XafTVF.Win` — WinForms UI
- TFM: `net10.0` (Win is `net10.0-windows`). DevExpress 25.2.\*, EF Core 10.0.0.
- Database: **SQL Server LocalDB**, hardcoded in `appsettings.json`. EasyTest builds (`#if EASYTEST`) swap to `XafTVFEasyTest`.
- Security: integrated mode with `PermissionPolicyUser` / `ApplicationUser`, lockout on, optimistic locking + deferred deletion.

Only auth/system tables exist today — business entities (Customer, Order, etc.) are added by the TVF spike.

## Build & run

- Build: `dotnet build XafTVF.slnx` from the repo root.
- Run Blazor: `dotnet run --project XafTVF\XafTVF.Blazor.Server`.
- Migrate DB: `dotnet run --project XafTVF\XafTVF.Blazor.Server -- --updateDatabase --forceUpdate --silent`.
- Run Win: build `XafTVF.Win` and launch the exe.

## Workflow rules

- **Always run `dotnet build XafTVF.slnx` before claiming work is done.** Type-check ≠ behavior, but a broken build is never "done".
- **Never run `dotnet ef migrations add` without asking.** Migrations are reviewed by hand on this repo; a PreToolUse hook blocks it as a safety net.
- **When creating or modifying XAF EF Core entities**, invoke the `xaf-efcore-entities` skill — virtual properties, BaseObjectInt, collection init, decimal precision, and DbContext registration are silent traps.
- **When creating or modifying XAF ViewControllers**, invoke the `xaf-viewcontroller-patterns` skill — OnActivated lifecycle, ObjectSpace disposal, and BoolList patterns matter.

## TVF spike gotchas

The pattern in `@TVF_PLAN.md` has two non-obvious requirements:

- **TVF SQL is not in EF Core migrations.** `dbo.fn_TopCustomersByRevenue` (and any other TVF) must be created by hand-rolled SQL or a raw-SQL migration step. `add-migration` will not generate it.
- **TVF result views must use `NonPersistentObjectSpace`.** Rows returned from the TVF are keyless; XAF's persistent ObjectSpace can't surface them. The trigger controller borrows a persistent ObjectSpace to execute the query, then maps results into a NonPersistentObjectSpace before opening the ListView.

Run `/tvf-spike` to walk through the full 10-step checklist on demand.

## Not a git repo (yet)

The root has no `.git` — git commands won't work. If you need history or branches, ask the user to `git init` first.
