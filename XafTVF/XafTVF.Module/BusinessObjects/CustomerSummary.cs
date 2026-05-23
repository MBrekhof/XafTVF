using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using System.ComponentModel;

namespace XafTVF.Module.BusinessObjects
{
    // Non-persistent XAF DTO that the result ListView binds to. Wraps a CustomerSummaryRow.
    // Created in a NonPersistentObjectSpace by the trigger controller after the TVF runs;
    // never read from or written to the database.
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
}
