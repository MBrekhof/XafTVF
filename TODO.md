# TODO

Working list for the XafTVF spike. Source of truth for the implementation steps is [TVF_PLAN.md](./TVF_PLAN.md) §"Order of work tomorrow" and `.claude/skills/tvf-spike/SKILL.md`.

## Infrastructure (done — 2026-05-23)

- [x] Scaffold XAF Blazor + Win + Module solution (initial commit `9b570d9`)
- [x] `dotnet build XafTVF.slnx` green (only transient NU1903 warnings)
- [x] LocalDB `XafTVF` created via `--updateDatabase --forceUpdate --silent` (14 baseline auth/system tables)
- [x] Blazor host serves HTTP 200 on `https://localhost:5001`
- [x] `ARCHITECTURE.md`, `TODO.md`, `SESSION_HANDOFF.md` written

## TVF spike — 10-step checklist

Stop and report after each non-trivial step so the user can sanity-check.

- [ ] **1.** Add `Customer` + `Order` entities in `XafTVF.Module/BusinessObjects/`. DbSets in `XafTVFEFCoreDbContext`, `HasPrecision(18,2)` on `Order.Total`, `AdditionalExportedTypes` in `XafTVFModule`. *(invoke `xaf-efcore-entities` first)*
- [ ] **2.** Apply the migration so the tables exist. **Ask before `dotnet ef migrations add`** — hook will block otherwise.
- [ ] **3.** Seed customers + orders in `XafTVF.Module/DatabaseUpdate/Updater.cs` using **Bogus** (add `Bogus` PackageReference to `XafTVF.Module.csproj`). Target ~1,000 customers × 20–100 orders each (≈60k orders) so the TVF aggregates a meaningful set server-side — that's where TVFs shine vs. loading rows into memory. Insert via bulk `AddRange` + `SaveChanges` batches; idempotent guard (skip if `Customers.Any()`).
- [x] **4.** ~~`HasDbFunction` + `Entity<>().HasNoKey().ToFunction(...)`~~ — **deviation from TVF_PLAN.md**: XAF's `DBUpdater` rejects any DbContext entity without a key. Instead, `CustomerSummaryRow` is a virtual-property POCO queried via `Database.SqlQuery<CustomerSummaryRow>($"... dbo.get_top_customers({topN}, {since})")`. `SqlQuery<T>` auto-registers T as a query type during model finalization → properties MUST be virtual. Use `[Column(TypeName = "decimal(18,2)")]` for precision; do NOT use `modelBuilder.Entity<>()` (promotes to full entity → XAF needs a key).
- [x] **5.** `dbo.get_top_customers` created via `CREATE OR ALTER FUNCTION` issued by `EnsureTvfExists()` in `Updater.cs` (idempotent, runs on each `--updateDatabase`). Filters `GCRecord = 0` on both `Customers` and `Orders` so XAF deferred deletion doesn't leak deleted rows into the aggregate.
- [x] **6.** End-to-end sanity check passed (temp diagnostic in Updater, removed after): top-5 query returns the same rows whether called via `ctx.GetTopCustomers(...)` in C# or `SELECT * FROM dbo.get_top_customers(5, ...)` via sqlcmd.
- [ ] **7.** Add `CustomerSummary` DTO + `TopCustomersParams` (`[DomainComponent]` + `NonPersistentBaseObject`, all properties virtual). Register both in `AdditionalExportedTypes`.
- [ ] **8.** Add `TopCustomersReportController` (WindowController, popup + execute) and `CustomerSummaryDrillThroughController`. *(invoke `xaf-viewcontroller-patterns` first)*
- [x] **9.** End-to-end verified via Playwright (artifacts in `test-artifacts/`). Two XAF Blazor-specific deviations from the plan applied:
  - **TargetWindow.NewModalWindow → TargetWindow.NewWindow** in trigger controller. The plan's `NewModalWindow` wraps the result view in a popup template with only AcceptAction (OK/Cancel) — no toolbar, no drill-through. `NewWindow` opens it as a full-window tab with the standard ListView toolbar.
  - **Drill-through action category "View" → `PredefinedCategory.RecordEdit`** in `CustomerSummaryDrillThroughController`. In XAF Blazor the "View" category folds into a "Navigation" dropdown that holds the navigation tree, not list actions. `RecordEdit` renders the action as inline per-row icons next to each list item (the canonical XAF Blazor drill-through pattern).
- [ ] **10.** *(Optional)* Convert to Variation A: inline params + results (no popup).

## Polish applied post-spike

- **Row-click on `CustomerSummary` no longer throws XAF error 1057.** `CustomerSummaryDrillThroughController.OnActivated` hooks `ListViewProcessCurrentObjectController.CustomProcessSelectedItem`, sets `e.Handled = true`, and reuses the same "open Customer DetailView" logic as the per-row icon. Verified by a second C# Playwright test case (`TopCustomersReport_RowClickIsInterceptedAndDrillsThrough`).

## Traps to keep in mind

- `CustomerSummaryRow` (EF row) stays out of `AdditionalExportedTypes`.
- `CustomerSummary` (DTO) gets no `DbSet<>`.
- All DTO/param properties **must** be `virtual`.
- Result view must use `NonPersistentObjectSpace` — `EFCoreObjectSpace` cannot surface keyless rows.
- Borrow persistent OS → cast to `EFCoreObjectSpace` → get `DbContext` → query → dispose → open result view.
- `AsNoTracking()` on the TVF query.
