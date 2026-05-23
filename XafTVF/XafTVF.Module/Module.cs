using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Model.Core;
using DevExpress.ExpressApp.Model.DomainLogics;
using DevExpress.ExpressApp.Model.NodeGenerators;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.ExpressApp.Updating;
using DevExpress.Persistent.Base;
using System.ComponentModel;

namespace XafTVF.Module
{
    // For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.ModuleBase.
    public sealed class XafTVFModule : ModuleBase
    {
        public XafTVFModule()
        {
            //
            // XafTVFModule
            //
            AdditionalExportedTypes.Add(typeof(XafTVF.Module.BusinessObjects.ApplicationUser));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.EF.PermissionPolicy.PermissionPolicyRole));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.EF.ModelDifference));
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.EF.ModelDifferenceAspect));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.SystemModule.SystemModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Security.SecurityModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Chart.ChartModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ConditionalAppearance.ConditionalAppearanceModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Dashboards.DashboardsModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Office.OfficeModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.PivotGrid.PivotGridModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ReportsV2.ReportsModuleV2));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.TreeListEditors.TreeListEditorsModuleBase));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.Validation.ValidationModule));
            RequiredModuleTypes.Add(typeof(DevExpress.ExpressApp.ViewVariantsModule.ViewVariantsModule));
            DevExpress.ExpressApp.Security.SecurityModule.UsedExportedTypes = DevExpress.Persistent.Base.UsedExportedTypes.Custom;
            AdditionalExportedTypes.Add(typeof(DevExpress.Persistent.BaseImpl.EF.HCategory));
            AdditionalExportedTypes.Add(typeof(XafTVF.Module.BusinessObjects.Customer));
            AdditionalExportedTypes.Add(typeof(XafTVF.Module.BusinessObjects.Order));
            AdditionalExportedTypes.Add(typeof(XafTVF.Module.BusinessObjects.CustomerSummary));
            AdditionalExportedTypes.Add(typeof(XafTVF.Module.BusinessObjects.TopCustomersParams));
            AdditionalExportedTypes.Add(typeof(XafTVF.Module.BusinessObjects.TopCustomersReportParams));
        }

        // Set in Setup(XafApplication). Used by TopCustomersReport.BeforePrint to grab the
        // EF Core DbContext without resorting to a service-locator antipattern at every call site.
        // XtraReport instances aren't created via DI so they can't take XafApplication as a ctor arg.
        public static XafApplication? CurrentApplication { get; private set; }

        public override IEnumerable<ModuleUpdater> GetModuleUpdaters(IObjectSpace objectSpace, Version versionFromDB)
        {
            ModuleUpdater updater = new DatabaseUpdate.Updater(objectSpace, versionFromDB);

            // Register the Top Customers Report so it shows up under the Reports navigation.
            // Stores a ReportDataV2 record on first --updateDatabase after the type is added.
            var reportsUpdater = new PredefinedReportsUpdater(Application, objectSpace, versionFromDB);
            reportsUpdater.AddPredefinedReport<XafTVF.Module.Reports.TopCustomersReport>(
                "Top Customers Report",
                typeof(XafTVF.Module.BusinessObjects.CustomerSummary),
                typeof(XafTVF.Module.BusinessObjects.TopCustomersReportParams));

            return new ModuleUpdater[] { updater, reportsUpdater };
        }
        public override void Setup(XafApplication application)
        {
            base.Setup(application);
            CurrentApplication = application;
        }
        public override void Setup(ApplicationModulesManager moduleManager)
        {
            base.Setup(moduleManager);
        }
    }
}
