# Session handoff

**Last updated:** 2026-05-23

## Where things are

TVF spike is **end-to-end working** — login → Tools tab → Top Customers Report popup → fill TopN/Since → SQL TVF executes → result list with `CustomerSummary` DTOs in a new tab → per-row drill-through to the real persistent `Customer` DetailView with its `Orders` collection.

Verified via Playwright (`test-artifacts/verify_tvf.py`, screenshots `01_*` through `08_*`).

### Steps complete (TODO.md)

- Infrastructure (md files, DB created, app boots)
- **1.** `Customer` + `Order` entities, DbContext, AdditionalExportedTypes
- **2.** Tables migrated (Customers, Orders with FK, decimal(18,2), GCRecord, OptimisticLockField)
- **3.** Bogus seeder: 1,000 customers, 59,484 orders, idempotent
- **4.** `CustomerSummaryRow` via `Database.SqlQuery<T>` (deviation from TVF_PLAN.md §Step 3 — see memory)
- **5.** `dbo.get_top_customers` created by `EnsureTvfExists()` (CREATE OR ALTER, GCRecord=0 filter)
- **6.** Sanity-check passed (temp diagnostic removed)
- **7.** `CustomerSummary` + `TopCustomersParams` DTOs (`[DomainComponent]` + `NonPersistentBaseObject`)
- **8.** `TopCustomersReportController` + `CustomerSummaryDrillThroughController`
- **9.** UI verified end-to-end. Two XAF Blazor deviations from TVF_PLAN.md applied — see TODO.md.

### Steps remaining

- **10.** *(Optional)* Variation A: inline params + results (no popup) — TVF_PLAN.md §Variations.

## XAF Blazor gotchas captured this session

Memory entries (`~/.claude/projects/C--Projects-XafTVF/memory/`):

- `xaf-efcore-bulk-seed.md` — `ctx.CreateProxy<T>()` required for bulk-insert of XAF EF Core entities.
- `xaf-tvf-row-mapping.md` — use `Database.SqlQuery<T>` for TVFs; `HasDbFunction` / `HasNoKey().ToFunction(...)` breaks XAF.
- `xaf-blazor-action-categories.md` — `"View"` category folds into Navigation dropdown; use `PredefinedCategory.RecordEdit` for per-row drill-through.
- `xaf-blazor-popup-result-window.md` — use `TargetWindow.NewWindow` (not `NewModalWindow`/`Default`) from a `PopupWindowShowAction.Execute`.

`TVF_PLAN.md` §Step 3 + Gotchas were amended for the `Database.SqlQuery<T>` deviation. The controller code in §Step 6/§Step 7 still shows `NewModalWindow` and `"View"` category — those are unchanged in the plan, but the actual checked-in controllers use `NewWindow` and `PredefinedCategory.RecordEdit` (the working choices).

## Useful commands

```powershell
# build
dotnet build XafTVF.slnx

# migrate DB + seed + ensure TVF (idempotent)
dotnet run --project XafTVF\XafTVF.Blazor.Server -- --updateDatabase --forceUpdate --silent

# run Blazor host
dotnet run --project XafTVF\XafTVF.Blazor.Server

# end-to-end UI verification (start server in background first)
python test-artifacts\verify_tvf.py

# inspect TVF result via raw SQL
sqlcmd -S "(localdb)\mssqllocaldb" -d XafTVF -Q "SELECT * FROM dbo.get_top_customers(5, '2023-01-01')"
```

## Next action

Either:
- Implement step 10 (inline-params variant) — straightforward DetailView controller change on `TopCustomersParams`, drop the popup.
- Or call the spike done and capture polish items (hide `Customer Summary` from main nav, sort result list by Revenue desc, hide the noisy `Customer Id` GUID column).
