using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using System.ComponentModel;

namespace XafTVF.Module.BusinessObjects
{
    // Parameters object for the Top Customers Report popup.
    // Lives in a NonPersistentObjectSpace owned by the popup DetailView; disposed when the popup closes.
    [DomainComponent]
    [ModelDefault("Caption", "Top Customers")]
    public class TopCustomersParams : NonPersistentBaseObject
    {
        [RuleRange(1, 1000)]
        public virtual int TopN { get; set; } = 10;

        [RuleRequiredField]
        public virtual DateTime Since { get; set; } = DateTime.Today.AddMonths(-3);

        // Reserved for the inline (no-popup) variant — hidden in the popup flow.
        [Browsable(false)]
        public virtual IList<CustomerSummary> Results { get; set; }
            = new BindingList<CustomerSummary>();
    }
}
