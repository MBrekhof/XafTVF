using System.ComponentModel;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.EFCore;
using DevExpress.ExpressApp.Templates;
using XafTVF.Module.BusinessObjects;

namespace XafTVF.Module.Controllers
{
    // Lives on the main window's toolbar (Tools group). Click → popup that asks for TopN/Since →
    // run dbo.get_top_customers via EF Core → map rows into a NonPersistentObjectSpace → open the
    // result ListView.
    public class TopCustomersReportController : WindowController
    {
        private readonly PopupWindowShowAction _runAction;

        public TopCustomersReportController()
        {
            TargetWindowType = WindowType.Main;

            _runAction = new PopupWindowShowAction(this, "RunTopCustomersReport", "Tools")
            {
                Caption = "Top Customers Report",
                ImageName = "BO_Report",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _runAction.CustomizePopupWindowParams += Action_CustomizePopup;
            _runAction.Execute += Action_Execute;
        }

        private void Action_CustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            // NPS owned by the popup DetailView — disposed when the popup closes.
            var paramsOs = Application.CreateObjectSpace(typeof(TopCustomersParams));
            var p = paramsOs.CreateObject<TopCustomersParams>();

            e.View = Application.CreateDetailView(paramsOs, p, isRoot: true);

            // SaveOnAccept would try to commit a non-persistent object — suppress it.
            e.DialogController.SaveOnAccept = false;
            e.DialogController.AcceptAction.Caption = "Run";
        }

        private void Action_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var p = (TopCustomersParams)e.PopupWindowViewCurrentObject;

            var rows = QueryTvf(p.TopN, p.Since);

            // NPS for the result ListView. The created ListView takes ownership and disposes it.
            var resultOs = (NonPersistentObjectSpace)
                Application.CreateObjectSpace(typeof(CustomerSummary));

            // ObjectsGetting fires when the ListView asks for its items — populate from the TVF rows.
            resultOs.ObjectsGetting += (_, args) =>
                args.Objects = new BindingList<CustomerSummary>(
                    rows.ConvertAll(r => MapToDto(resultOs, r)));

            var listView = Application.CreateListView(
                Application.FindListViewId(typeof(CustomerSummary)),
                new CollectionSource(resultOs, typeof(CustomerSummary)),
                isRoot: true);

            listView.Caption = $"Top {p.TopN} customers since {p.Since:yyyy-MM-dd}";

            e.ShowViewParameters.CreatedView = listView;
            // NewWindow opens the result in a new top-level window with a full toolbar so the View-group
            // "Open Customer" drill-through action is reachable. NewModalWindow would frame as a popup
            // (limited toolbar). Default still inherits the popup template when triggered from a
            // PopupWindowShowAction.Execute, so the result also renders inside the popup frame.
            e.ShowViewParameters.TargetWindow = TargetWindow.NewWindow;
            e.ShowViewParameters.Context = TemplateContext.View;
        }

        private List<CustomerSummaryRow> QueryTvf(int topN, DateTime since)
        {
            // Borrow a short-lived persistent ObjectSpace just to get the DbContext, then dispose.
            using var efOs = Application.CreateObjectSpace(typeof(Customer));
            var ctx = (XafTVFEFCoreDbContext)((EFCoreObjectSpace)efOs).DbContext;
            // No AsNoTracking() needed — Database.SqlQuery<T> rows are not tracked.
            return ctx.GetTopCustomers(topN, since).ToList();
        }

        private static CustomerSummary MapToDto(IObjectSpace os, CustomerSummaryRow r)
        {
            var dto = os.CreateObject<CustomerSummary>();
            dto.CustomerId = r.CustomerId;
            dto.Name = r.Name;
            dto.Revenue = r.Revenue;
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
}
