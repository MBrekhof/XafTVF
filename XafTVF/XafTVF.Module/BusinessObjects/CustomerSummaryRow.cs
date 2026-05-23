using System.ComponentModel.DataAnnotations.Schema;

namespace XafTVF.Module.BusinessObjects
{
    // Keyless EF query type bound to the SQL TVF dbo.get_top_customers, materialized via
    // Database.SqlQuery<CustomerSummaryRow>(...). NOT a XAF business object: do NOT add to
    // AdditionalExportedTypes and do NOT add a DbSet<>. XAF only sees the CustomerSummary
    // non-persistent DTO that wraps these rows.
    //
    // Properties must be virtual: EF Core auto-registers T as a query type during model
    // finalization, and the change-tracking proxy rewriter scans it.
    //
    // [Column(TypeName = "decimal(18,2)")] silences the missing-precision warning without
    // requiring modelBuilder.Entity<>() — touching this type via modelBuilder promotes it
    // from "query type" to a full entity needing a key, which XAF would reject.
    public class CustomerSummaryRow
    {
        public virtual Guid CustomerId { get; set; }
        public virtual string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public virtual decimal Revenue { get; set; }

        public virtual int OrderCount { get; set; }
    }
}
