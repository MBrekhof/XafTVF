# Table-Valued Functions in XAF (EF Core 10)

Reference notes for the XafTVF spike. Tested target stack: XAF (DevExpress) Blazor.Server + Win + Module, EF Core 10, existing DbContext `XafTVFEFCoreDbContext`.

## The core idea

EF Core can map a TVF and return `IQueryable<TRow>`. XAF, however, can't surface that directly because:

- TVF rows are typically **keyless** — XAF's UI pipeline expects keyed business objects.
- TVFs usually take **parameters** — XAF ListView doesn't know how to ask for them.
- TVF result rows are **read-only** — editing in place would mislead users.

Bridge with a **non-persistent XAF DTO** and a **`PopupWindowShowAction`** that prompts for parameters, runs the TVF, builds a `NonPersistentObjectSpace`, and opens a ListView of the results.

## Architecture at a glance

```
[User]                    [Trigger Controller]              [DbContext / TVF]
   │ click "Top Customers"        │                               │
   ├─────────────────────────────►│                               │
   │                              │ CustomizePopupWindowParams    │
   │                              │  → param DetailView (NPS)     │
   │◄─── popup with params ───────┤                               │
   │                              │                               │
   │ fill TopN, Since, OK         │                               │
   ├─────────────────────────────►│                               │
   │                              │ Execute:                      │
   │                              │   1. Borrow persistent OS     │
   │                              │   2. Cast to EF DbContext ────►
   │                              │   3. Call GetTopCustomers() ──►
   │                              │◄────── List<TvfRow> ──────────│
   │                              │   4. Map to DTOs in NPS       │
   │                              │   5. ShowViewParameters       │
   │◄─── result ListView ─────────┤                               │
```

Lifetimes:

| Object                   | OS scope                          | Disposal                              |
|--------------------------|-----------------------------------|---------------------------------------|
| `TopCustomersParams`     | Popup view's NPS                  | Auto-disposed when popup closes       |
| EF `DbContext` query     | Short-lived `using` in `Execute`  | Disposed before result view opens     |
| `CustomerSummary` rows   | Result view's NPS                 | Disposed when result view closes      |
| Drilled-through `Customer` | Fresh persistent OS             | Disposed when its DetailView closes   |

No leaks, no static state.

---

## Step 1 — Define a Customer entity to query against (placeholder)

We don't yet have a sample persistent entity to aggregate. Add a minimal `Customer` + `Order` for the spike (in `XafTVF.Module/BusinessObjects/`).

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;

namespace XafTVF.Module.BusinessObjects;

[DefaultClassOptions]
[DefaultProperty(nameof(Name))]
public class Customer : BaseObject
{
    public virtual string Name { get; set; } = string.Empty;

    [Aggregated]
    public virtual IList<Order> Orders { get; set; } = new ObservableCollection<Order>();
}

[DefaultClassOptions]
public class Order : BaseObject
{
    public virtual DateTime OrderDate { get; set; }
    public virtual decimal Total { get; set; }

    public virtual Guid? CustomerId { get; set; }
    [ForeignKey(nameof(CustomerId))]
    public virtual Customer? Customer { get; set; }
}
```

Add to `XafTVFEFCoreDbContext`:

```csharp
public DbSet<Customer> Customers { get; set; }
public DbSet<Order> Orders { get; set; }
```

Add to `OnModelCreating`:

```csharp
modelBuilder.Entity<Order>().Property(p => p.Total).HasPrecision(18, 2);
```

Add to `XafTVFModule` ctor:

```csharp
AdditionalExportedTypes.Add(typeof(Customer));
AdditionalExportedTypes.Add(typeof(Order));
```

---

## Step 2 — Create the TVF in the database

For SQL Server (adjust syntax for your DB):

```sql
CREATE OR ALTER FUNCTION dbo.get_top_customers
(
    @top_n int,
    @since datetime2
)
RETURNS TABLE
AS
RETURN
    SELECT TOP (@top_n)
           c.Id          AS CustomerId,
           c.Name        AS Name,
           SUM(o.Total)  AS Revenue,
           COUNT(*)      AS OrderCount
    FROM   dbo.Customers c
    JOIN   dbo.Orders    o ON o.CustomerId = c.Id
    WHERE  o.OrderDate >= @since
    GROUP BY c.Id, c.Name
    ORDER BY SUM(o.Total) DESC;
```

For PostgreSQL:

```sql
CREATE OR REPLACE FUNCTION public.get_top_customers(
    top_n  integer,
    since  timestamp
)
RETURNS TABLE (
    "CustomerId"  uuid,
    "Name"        text,
    "Revenue"     numeric(18,2),
    "OrderCount"  integer
)
LANGUAGE sql
AS $$
    SELECT  c."Id"                AS "CustomerId",
            c."Name"              AS "Name",
            SUM(o."Total")::numeric(18,2)  AS "Revenue",
            COUNT(*)::int         AS "OrderCount"
    FROM    public."Customers" c
    JOIN    public."Orders"    o ON o."CustomerId" = c."Id"
    WHERE   o."OrderDate" >= since
    GROUP BY c."Id", c."Name"
    ORDER BY SUM(o."Total") DESC
    LIMIT   top_n;
$$;
```

We'll create this manually (or via a migration) outside EF Core's model snapshot.

---

## Step 3 — Map the TVF in `XafTVFEFCoreDbContext`

The raw EF row type (`CustomerSummaryRow`) is **not** a XAF business class — keep it out of `AdditionalExportedTypes`.

```csharp
// Inside XafTVFEFCoreDbContext

public IQueryable<CustomerSummaryRow> GetTopCustomers(int topN, DateTime since)
    => FromExpression(() => GetTopCustomers(topN, since));

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // ... existing setup ...

    modelBuilder
        .HasDbFunction(typeof(XafTVFEFCoreDbContext)
            .GetMethod(nameof(GetTopCustomers))!)
        .HasName("get_top_customers"); // or schema-qualified e.g. "dbo.get_top_customers"

    modelBuilder.Entity<CustomerSummaryRow>(b =>
    {
        b.HasNoKey();
        b.ToFunction("get_top_customers");
        b.Property(p => p.Revenue).HasPrecision(18, 2);
    });
}
```

The keyless row type:

```csharp
namespace XafTVF.Module.BusinessObjects;

public class CustomerSummaryRow
{
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}
```

---

## Step 4 — Non-persistent XAF DTO (what the ListView shows)

```csharp
using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;

namespace XafTVF.Module.BusinessObjects;

[DomainComponent]
[DefaultClassOptions]
[DefaultProperty(nameof(Name))]
[ModelDefault("AllowEdit", "False")]
[ModelDefault("AllowDelete", "False")]
[ModelDefault("AllowNew", "False")]
public class CustomerSummary : NonPersistentBaseObject
{
    public virtual Guid CustomerId { get; set; }
    public virtual string Name { get; set; } = string.Empty;
    public virtual decimal Revenue { get; set; }
    public virtual int OrderCount { get; set; }
}
```

Register in `XafTVFModule`:

```csharp
AdditionalExportedTypes.Add(typeof(CustomerSummary));
AdditionalExportedTypes.Add(typeof(TopCustomersParams));
```

---

## Step 5 — Param object (non-persistent, with validation)

```csharp
using System.ComponentModel;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;

namespace XafTVF.Module.BusinessObjects;

[DomainComponent]
[ModelDefault("Caption", "Top Customers")]
public class TopCustomersParams : NonPersistentBaseObject
{
    [RuleRange(1, 1000)]
    public virtual int TopN { get; set; } = 10;

    [RuleRequiredField]
    public virtual DateTime Since { get; set; } = DateTime.Today.AddMonths(-3);

    // For the inline-results variant (see "Variations" below). Hidden by default.
    [Browsable(false)]
    public virtual IList<CustomerSummary> Results { get; set; }
        = new BindingList<CustomerSummary>();
}
```

---

## Step 6 — Trigger controller (popup + execute)

`WindowController` so the action is reachable from the main window's toolbar (Tools group).

Place in `XafTVF.Module/Controllers/TopCustomersReportController.cs`:

```csharp
using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.EFCore;
using DevExpress.ExpressApp.Templates;
using Microsoft.EntityFrameworkCore;
using XafTVF.Module.BusinessObjects;

namespace XafTVF.Module.Controllers;

public class TopCustomersReportController : WindowController
{
    private readonly PopupWindowShowAction _runAction;

    public TopCustomersReportController()
    {
        TargetWindowType = WindowType.Main;

        _runAction = new PopupWindowShowAction(this, "RunTopCustomers", "Tools")
        {
            Caption = "Top Customers Report",
            ImageName = "BO_Report",
            SelectionDependencyType = SelectionDependencyType.Independent
        };

        _runAction.CustomizePopupWindowParams += Action_CustomizePopup;
        _runAction.Execute += Action_Execute;
    }

    private void Action_CustomizePopup(
        object sender, CustomizePopupWindowParamsEventArgs e)
    {
        var os = Application.CreateObjectSpace(typeof(TopCustomersParams));
        var p  = os.CreateObject<TopCustomersParams>();

        e.View = Application.CreateDetailView(os, p, isRoot: true);

        // We don't want XAF trying to commit a non-persistent object on accept
        e.DialogController.SaveOnAccept = false;
        e.DialogController.AcceptAction.Caption = "Run";
    }

    private void Action_Execute(
        object sender, PopupWindowShowActionExecuteEventArgs e)
    {
        var p = (TopCustomersParams)e.PopupWindowViewCurrentObject;

        var rows = QueryTvf(p.TopN, p.Since);

        var resultOs = (NonPersistentObjectSpace)
            Application.CreateObjectSpace(typeof(CustomerSummary));

        resultOs.ObjectsGetting += (_, args) =>
            args.Objects = new BindingList<CustomerSummary>(
                rows.Select(r => MapToDto(resultOs, r)).ToList());

        var listView = Application.CreateListView(
            Application.FindListViewId(typeof(CustomerSummary)),
            new CollectionSource(resultOs, typeof(CustomerSummary)),
            isRoot: true);

        listView.Caption = $"Top {p.TopN} customers since {p.Since:yyyy-MM-dd}";

        e.ShowViewParameters.CreatedView  = listView;
        e.ShowViewParameters.TargetWindow = TargetWindow.NewModalWindow;
        e.ShowViewParameters.Context      = TemplateContext.View;
    }

    private List<CustomerSummaryRow> QueryTvf(int topN, DateTime since)
    {
        using var efOs = Application.CreateObjectSpace(typeof(Customer));
        var ctx = (XafTVFEFCoreDbContext)((EFCoreObjectSpace)efOs).DbContext;

        return ctx.GetTopCustomers(topN, since)
                  .AsNoTracking()
                  .ToList();
    }

    private static CustomerSummary MapToDto(IObjectSpace os, CustomerSummaryRow r)
    {
        var dto = os.CreateObject<CustomerSummary>();
        dto.CustomerId = r.CustomerId;
        dto.Name       = r.Name;
        dto.Revenue    = r.Revenue;
        dto.OrderCount = r.OrderCount;
        return dto;
    }

    protected override void OnDeactivated()
    {
        _runAction.CustomizePopupWindowParams -= Action_CustomizePopup;
        _runAction.Execute -= Action_Execute;
        base.OnDeactivated();
    }
}
```

---

## Step 7 — Drill-through to the real Customer

Place in `XafTVF.Module/Controllers/CustomerSummaryDrillThroughController.cs`:

```csharp
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using XafTVF.Module.BusinessObjects;

namespace XafTVF.Module.Controllers;

public class CustomerSummaryDrillThroughController
    : ObjectViewController<ListView, CustomerSummary>
{
    private readonly SimpleAction _openAction;

    public CustomerSummaryDrillThroughController()
    {
        _openAction = new SimpleAction(this, "OpenCustomer", "View")
        {
            Caption = "Open Customer",
            ImageName = "Action_Navigation",
            SelectionDependencyType = SelectionDependencyType.RequireSingleObject
        };
        _openAction.Execute += OpenAction_Execute;
    }

    private void OpenAction_Execute(object sender, SimpleActionExecuteEventArgs e)
    {
        var summary = (CustomerSummary)e.CurrentObject;

        var os = Application.CreateObjectSpace(typeof(Customer));
        var customer = os.GetObjectByKey<Customer>(summary.CustomerId);
        if (customer == null) return;

        var dv = Application.CreateDetailView(os, customer, isRoot: true);
        e.ShowViewParameters.CreatedView  = dv;
        e.ShowViewParameters.TargetWindow = TargetWindow.NewWindow;
        e.ShowViewParameters.Context      = TemplateContext.View;
    }

    protected override void OnDeactivated()
    {
        _openAction.Execute -= OpenAction_Execute;
        base.OnDeactivated();
    }
}
```

---

## Variations

### A. Inline params + results (no popup)

Skip the popup. Keep `TopCustomersParams.Results` visible and add `[ImmediatePostData]` to `TopN` and `Since`. In a ViewController on `TopCustomersParams` DetailView, hook `ObjectSpace.ObjectChanged` (or `View.CurrentObject.PropertyChanged`) and re-fill `Results` whenever a param changes. UX: one DetailView with params on top, nested ListView of results below.

### B. Params from selected object

Change the action's `TargetObjectType` to e.g. `Region`, set `SelectionDependencyType = RequireSingleObject`, then in `CustomizePopup` pre-fill the param object from `((ObjectView)View).CurrentObject`.

### C. Saved parameter sets

Promote `TopCustomersParams` to a real persistent entity. Popup flow unchanged. Saved param sets now appear as their own ListView under Navigation and users can re-run any of them.

---

## Gotchas (XAF + EF Core 10)

- **Don't** add `CustomerSummaryRow` (the keyless EF type) to `AdditionalExportedTypes` — only the DTO `CustomerSummary` is XAF-visible.
- **Don't** add `DbSet<CustomerSummary>` — `CustomerSummary` is a non-persistent DTO, not an EF entity.
- All `CustomerSummary` properties must be `virtual` (XAF change-tracking proxy requirement).
- Set decimal precision on `CustomerSummaryRow.Revenue` in `OnModelCreating` — otherwise silent truncation.
- `AsNoTracking()` on the TVF query — rows are read-only anyway.
- PostgreSQL function/column names are case-sensitive when quoted; keep the function name lower_snake_case unquoted, but if you need quoted column names use them consistently in the function body.
- TVF won't appear in EF migrations automatically — manage it via raw SQL migration or DBA process.
- `ToFunction(...)` is what binds the keyless entity to the function; without it EF tries to resolve a table named `CustomerSummaryRow`.

---

## Order of work tomorrow

1. Add `Customer` and `Order` entities + DbSets + decimal precision + `AdditionalExportedTypes`.
2. Run migration so tables exist.
3. Seed a handful of customers/orders in `DatabaseUpdate/Updater.cs` (or via the UI).
4. Add `CustomerSummaryRow` + `HasDbFunction` + `ToFunction` mapping in DbContext.
5. Create the TVF in the database (raw SQL).
6. Sanity-check with a quick `ctx.GetTopCustomers(10, DateTime.Today.AddYears(-1)).ToList()` from a controller breakpoint.
7. Add `CustomerSummary` DTO + `TopCustomersParams` + register both.
8. Add `TopCustomersReportController` and `CustomerSummaryDrillThroughController`.
9. Run Blazor.Server, click "Top Customers Report", fill params, verify list + drill-through.
10. If green: try Variation A (inline) to compare UX.
