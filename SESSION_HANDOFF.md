# Session handoff

**Last updated:** 2026-05-23 (end of build session)

## Status

Spike is **complete and public** at <https://github.com/MBrekhof/XafTVF>. Both TVF flows work end-to-end and have automated coverage. Latest commit on `main`: `4a2eec9`.

## What works

- **In-app popup flow** (`Tools → Top Customers Report → fill params → Run → result list → drill-through to Customer DetailView`). Row click on a result row is intercepted via `ListViewProcessCurrentObjectController.CustomProcessSelectedItem` so the non-persistent CustomerSummary doesn't trip XAF error 1057.
- **Predefined XtraReports flow** (`Reports → Reports → Top Customers Report → param dialog → Preview`). The XtraReport reads its params from `Parameters["XafReportParametersObject"].Value` (XAF passes the whole `ReportParametersObjectBase` as a single hidden parameter) and binds the TVF result to `DataSource`.
- **Bogus seeder** (~1k customers / ~60k orders, idempotent). `dbo.get_top_customers` recreated via `CREATE OR ALTER` on every `--updateDatabase`.
- **3 C# Playwright tests** (`XafTVF.UITests` — `Microsoft.Playwright.Xunit`), all green when run together:
  1. `TopCustomersReport_RunsAndDrillsThroughViaIcon`
  2. `TopCustomersReport_RowClickIsInterceptedAndDrillsThrough`
  3. `TopCustomersReport_PredefinedXtraReport_RendersInPreview`
- **README + architecture diagram** at `docs/architecture.png` (editable source at `docs/architecture.excalidraw`).

## Open items (TODO.md "Open items" section)

- [ ] **Perf comparison test** — benchmark TVF vs EF-translated LINQ vs client-side aggregation over the existing seed, write results to `docs/perf.md`. This validates the README's "order of magnitude" claim.
- [ ] *(Optional, step 10)* — Variation A: inline params + results (no popup) — see TVF_PLAN.md §Variations.

## Recent commits (newest first)

```
4a2eec9 TODO: add perf comparison test for TVF vs LINQ vs client-side
1a4e418 README: add 'Why this exists' section
ffc8070 Add README with architecture diagram
7445277 Add Top Customers XtraReport (predefined, bound to the TVF)
7404f2e Intercept CustomerSummary row-click to avoid XAF error 1057
2ae4b30 Add C# Playwright xUnit harness for the TVF spike
3d91d78 Implement TVF → NonPersistentObject XAF spike end-to-end
9b570d9 Initial commit: XAF TVF spike scaffolding
```

## Memory updates this session

Captured in `~/.claude/projects/C--Projects-XafTVF/memory/`:

- `xaf-efcore-bulk-seed.md` — use `ctx.CreateProxy<T>()` in Bogus `CustomInstantiator`; `new T()` trips `INotifyPropertyChanging`.
- `xaf-tvf-row-mapping.md` — query TVFs via `Database.SqlQuery<T>($"...")`; canonical `HasDbFunction` + `HasNoKey().ToFunction(...)` is rejected by XAF's DBUpdater.
- `xaf-blazor-action-categories.md` — use `PredefinedCategory.RecordEdit` for per-row drill-through icons (XAF Blazor folds `"View"` into a Navigation dropdown).
- `xaf-blazor-popup-result-window.md` — from `PopupWindowShowAction.Execute`, use `TargetWindow.NewWindow` (NOT `NewModalWindow`/`Default`).
- `xaf-nonpersistent-row-click.md` — intercept row-click error 1057 via `ListViewProcessCurrentObjectController.CustomProcessSelectedItem`.
- `xaf-reportsv2-param-object.md` — read params from `Parameters["XafReportParametersObject"].Value`, XAF doesn't auto-bind individual properties.
- `xaf-blazor-url-routing.md` — `https://host/<ViewId>` (e.g. `/ReportDataV2_ListView`) for deterministic test navigation.
- `prefer-csharp.md` — new tests/scripts/tooling go in C#; existing `verify_tvf.py` stays but isn't extended.

## Useful commands

```powershell
# build
dotnet build XafTVF.slnx

# migrate DB + seed + ensure TVF (idempotent)
dotnet run --project XafTVF\XafTVF.Blazor.Server -- --updateDatabase --forceUpdate --silent

# run Blazor host
dotnet run --project XafTVF\XafTVF.Blazor.Server

# UI tests (Blazor must be running)
dotnet test XafTVF\XafTVF.UITests\XafTVF.UITests.csproj

# inspect TVF via raw SQL
sqlcmd -S "(localdb)\mssqllocaldb" -d XafTVF -Q "SELECT * FROM dbo.get_top_customers(5, '2023-01-01')"
```

## Heads up for deployment

`appsettings.json` carries the default DevExpress scaffolding `UrlSigningKey` (`64D0D174-387C-4836-9509-03B271E03496`). Sandbox-fine, but rotate via user secrets / env if this ever gets deployed.
