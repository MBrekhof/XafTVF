using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace XafTVF.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(Name))]
    public class Customer : BaseObject
    {
        public virtual string Name { get; set; } = string.Empty;

        [Aggregated]
        public virtual IList<Order> Orders { get; set; } = new ObservableCollection<Order>();
    }
}
