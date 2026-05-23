using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using XafTVF.Module.BusinessObjects;

namespace XafTVF.Module.Controllers
{
    // Active on the result ListView. Provides two paths to the same drill-through:
    //   1. Per-row "Open Customer" button (RecordEdit category — renders as inline row icon).
    //   2. Row click / double-click / Enter on a CustomerSummary row.
    //
    // Without (2), XAF's default ListViewProcessCurrentObjectController fires and tries to open
    // CustomerSummary as a DetailView. Because CustomerSummary is non-persistent, that throws
    // XAF error 1057 ("A newly created record cannot be shown until it is saved"). We intercept
    // and reroute to the same Customer DetailView the icon opens.
    //
    // The original plan put the action in the "View" category; XAF Blazor folds that into a
    // Navigation dropdown (nav tree, not list actions), so RecordEdit is used instead.
    public class CustomerSummaryDrillThroughController
        : ObjectViewController<ListView, CustomerSummary>
    {
        private readonly SimpleAction _openAction;
        private ListViewProcessCurrentObjectController? _processController;

        public CustomerSummaryDrillThroughController()
        {
            _openAction = new SimpleAction(this, "OpenCustomer", PredefinedCategory.RecordEdit)
            {
                Caption = "Open Customer",
                ImageName = "Action_Navigation",
                SelectionDependencyType = SelectionDependencyType.RequireSingleObject
            };
            _openAction.Execute += OpenAction_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _processController = Frame.GetController<ListViewProcessCurrentObjectController>();
            if (_processController != null)
            {
                _processController.CustomProcessSelectedItem += ProcessController_CustomProcessSelectedItem;
            }
        }

        protected override void OnDeactivated()
        {
            if (_processController != null)
            {
                _processController.CustomProcessSelectedItem -= ProcessController_CustomProcessSelectedItem;
                _processController = null;
            }
            _openAction.Execute -= OpenAction_Execute;
            base.OnDeactivated();
        }

        private void OpenAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            OpenCustomerFor((CustomerSummary)e.CurrentObject, e.ShowViewParameters);
        }

        private void ProcessController_CustomProcessSelectedItem(
            object sender, CustomProcessListViewSelectedItemEventArgs e)
        {
            // Suppress the default "open DetailView for the selected object" behavior, which
            // would try to open the non-persistent CustomerSummary and trigger XAF error 1057.
            e.Handled = true;
            OpenCustomerFor((CustomerSummary)e.InnerArgs.CurrentObject, e.InnerArgs.ShowViewParameters);
        }

        private void OpenCustomerFor(CustomerSummary summary, ShowViewParameters svp)
        {
            // Fresh persistent OS owned by the created DetailView — disposed when the view closes.
            var os = Application.CreateObjectSpace(typeof(Customer));
            var customer = os.GetObjectByKey<Customer>(summary.CustomerId);
            if (customer == null) { os.Dispose(); return; }

            svp.CreatedView = Application.CreateDetailView(os, customer, isRoot: true);
            svp.TargetWindow = TargetWindow.NewWindow;
            svp.Context = TemplateContext.View;
        }
    }
}
