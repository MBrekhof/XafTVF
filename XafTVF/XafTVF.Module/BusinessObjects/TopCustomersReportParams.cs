using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace XafTVF.Module.BusinessObjects
{
    // Parameter object shown by XAF's ReportsV2 module before the Top Customers Report renders.
    // Mirrors TopCustomersParams (used by the in-app popup flow) but inherits from
    // ReportParametersObjectBase so XAF's report engine knows how to bind it.
    //
    // GetCriteria returns null — we don't filter persistent data, we re-execute the TVF with
    // these param values in TopCustomersReport.BeforePrint.
    [DomainComponent]
    [ModelDefault("Caption", "Top Customers Report")]
    public class TopCustomersReportParams : ReportParametersObjectBase
    {
        private readonly IObjectSpaceCreator _objectSpaceCreator;

        public TopCustomersReportParams(IObjectSpaceCreator objectSpaceCreator)
            : base(objectSpaceCreator)
        {
            _objectSpaceCreator = objectSpaceCreator;
        }

        [RuleRange(1, 1000)]
        public virtual int TopN { get; set; } = 10;

        [RuleRequiredField]
        public virtual DateTime Since { get; set; } = DateTime.Today.AddMonths(-3);

        // The base ctor stores IObjectSpaceCreator but does not implement CreateObjectSpace —
        // delegate to the creator so XAF can build the param DetailView's ObjectSpace.
        // IObjectSpaceCreator : IObjectSpaceFactory whose CreateObjectSpace takes the target Type.
        protected override IObjectSpace CreateObjectSpace()
            => _objectSpaceCreator.CreateObjectSpace(typeof(TopCustomersReportParams));

        public override CriteriaOperator GetCriteria() => null!;

        public override SortProperty[] GetSorting() => Array.Empty<SortProperty>();
    }
}
