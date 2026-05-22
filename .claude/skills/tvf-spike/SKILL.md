---
name: tvf-spike
description: Walk through the 10-step TVF → NonPersistentObject spike checklist from TVF_PLAN.md. Use when the user invokes /tvf-spike or asks to implement, continue, or verify the TVF spike pattern (mapping a SQL table-valued function to a XAF non-persistent DTO with a PopupWindowShowAction trigger).
---

# TVF spike workflow

The full plan lives in `@TVF_PLAN.md` (architecture, code samples, SQL for both SQL Server and PostgreSQL, gotchas, variations). Read it before starting if you haven't already this session.

## Before you touch code

- Invoke `xaf-efcore-entities` before adding `Customer`, `Order`, or `CustomerSummaryRow`.
- Invoke `xaf-viewcontroller-patterns` before adding `TopCustomersReportController` or `CustomerSummaryDrillThroughController`.

## Checklist (TVF_PLAN.md §"Order of work tomorrow")

Work through these in order. Stop and report after each non-trivial step so the user can sanity-check.

1. **Add `Customer` + `Order` entities** in `XafTVF.Module/BusinessObjects/`. Include DbSets in `XafTVFEFCoreDbContext`, `HasPrecision(18,2)` on `Order.Total`, and `AdditionalExportedTypes` registration in `XafTVFModule`.
2. **Run the migration** so the tables exist. **Do not run `dotnet ef migrations add` without asking the user first** — a PreToolUse hook will block it anyway.
3. **Seed a few customers and orders** in `DatabaseUpdate/Updater.cs` (or by hand via the UI).
4. **Add `CustomerSummaryRow`** (keyless EF row type) plus `HasDbFunction` + `ToFunction("get_top_customers")` mapping in `OnModelCreating`. Do **not** add it to `AdditionalExportedTypes`.
5. **Create the TVF in the database** with raw SQL (`dbo.get_top_customers` — see TVF_PLAN.md §Step 2). EF migrations will not generate this.
6. **Sanity-check** with `ctx.GetTopCustomers(10, DateTime.Today.AddYears(-1)).ToList()` from a controller breakpoint or temp test.
7. **Add `CustomerSummary` DTO and `TopCustomersParams`** (both `[DomainComponent]` + `NonPersistentBaseObject`, all properties `virtual`). Register both in `AdditionalExportedTypes`.
8. **Add the two controllers**: `TopCustomersReportController` (`WindowController`, popup + execute) and `CustomerSummaryDrillThroughController` (drill from summary row to real Customer).
9. **Build and verify**: `dotnet build XafTVF.slnx`, then `dotnet run --project XafTVF\XafTVF.Blazor.Server`. Click "Top Customers Report" in the Tools group, fill params, confirm the result ListView opens and drill-through works.
10. **Optional Variation A**: convert to inline params + results (no popup) — see TVF_PLAN.md §Variations.

## Non-obvious traps to repeat back

- `CustomerSummaryRow` (EF row) is NOT XAF-visible — keep it out of `AdditionalExportedTypes`.
- `CustomerSummary` (DTO) is NOT an EF entity — do not add a `DbSet<CustomerSummary>`.
- All DTO/param properties must be `virtual`.
- Result view must be backed by `NonPersistentObjectSpace` — `EFCoreObjectSpace` cannot surface keyless rows.
- Borrow the persistent ObjectSpace, cast to `EFCoreObjectSpace` to get the `DbContext`, run the query, then dispose before opening the result view.
- `AsNoTracking()` on the TVF query.
