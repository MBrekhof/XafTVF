using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using XafTVF.Module.BusinessObjects;

namespace XafTVF.Module.Controllers
{
    // Active on the result ListView. Adds "Open Customer" to the RecordEdit group so it appears
    // as a standalone button in the ListView's main toolbar (next to New/Delete). The original
    // plan used the "View" category, but XAF Blazor folds "View" actions into a Navigation
    // dropdown that holds the navigation tree, not list-action buttons.
    public class CustomerSummaryDrillThroughController
        : ObjectViewController<ListView, CustomerSummary>
    {
        private readonly SimpleAction _openAction;

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

        private void OpenAction_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var summary = (CustomerSummary)e.CurrentObject;

            // Fresh persistent OS owned by the created DetailView — disposed when the view closes.
            var os = Application.CreateObjectSpace(typeof(Customer));
            var customer = os.GetObjectByKey<Customer>(summary.CustomerId);
            if (customer == null) { os.Dispose(); return; }

            var dv = Application.CreateDetailView(os, customer, isRoot: true);
            e.ShowViewParameters.CreatedView = dv;
            e.ShowViewParameters.TargetWindow = TargetWindow.NewWindow;
            e.ShowViewParameters.Context = TemplateContext.View;
        }

        protected override void OnDeactivated()
        {
            _openAction.Execute -= OpenAction_Execute;
            base.OnDeactivated();
        }
    }
}
