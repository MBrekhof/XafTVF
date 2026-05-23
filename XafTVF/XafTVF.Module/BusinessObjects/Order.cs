using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using System.ComponentModel.DataAnnotations.Schema;

#nullable enable

namespace XafTVF.Module.BusinessObjects
{
    [DefaultClassOptions]
    public class Order : BaseObject
    {
        public virtual DateTime OrderDate { get; set; }
        public virtual decimal Total { get; set; }

        public virtual Guid? CustomerId { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }
    }
}
