using DevExpress.ExpressApp.Design;
using DevExpress.ExpressApp.EFCore.DesignTime;
using DevExpress.ExpressApp.EFCore.Updating;
using DevExpress.Persistent.BaseImpl.EF;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace XafTVF.Module.BusinessObjects
{
    [TypesInfoInitializer(typeof(DbContextTypesInfoInitializer<XafTVFEFCoreDbContext>))]
    public class XafTVFEFCoreDbContext : DbContext
    {
        public XafTVFEFCoreDbContext(DbContextOptions<XafTVFEFCoreDbContext> options) : base(options)
        {
        }
        //public DbSet<ModuleInfo> ModulesInfo { get; set; }
        public DbSet<ModelDifference> ModelDifferences { get; set; }
        public DbSet<ModelDifferenceAspect> ModelDifferenceAspects { get; set; }
        public DbSet<PermissionPolicyRole> Roles { get; set; }
        public DbSet<XafTVF.Module.BusinessObjects.ApplicationUser> Users { get; set; }
        public DbSet<XafTVF.Module.BusinessObjects.ApplicationUserLoginInfo> UserLoginsInfo { get; set; }
        public DbSet<ReportDataV2> ReportDataV2 { get; set; }
        public DbSet<DashboardData> DashboardData { get; set; }
        public DbSet<HCategory> HCategories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }

        // Queries dbo.get_top_customers via Database.SqlQuery so CustomerSummaryRow
        // stays out of the EF model (and out of XAF's business-class scan).
        public IQueryable<CustomerSummaryRow> GetTopCustomers(int topN, DateTime since)
            => Database.SqlQuery<CustomerSummaryRow>(
                $"SELECT CustomerId, Name, Revenue, OrderCount FROM dbo.get_top_customers({topN}, {since})");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.UseDeferredDeletion(this);
            modelBuilder.UseOptimisticLock();
            modelBuilder.SetOneToManyAssociationDeleteBehavior(DeleteBehavior.SetNull, DeleteBehavior.Cascade);
            modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues);
            modelBuilder.UsePropertyAccessMode(PropertyAccessMode.PreferFieldDuringConstruction);
            modelBuilder.Entity<XafTVF.Module.BusinessObjects.ApplicationUserLoginInfo>(b =>
            {
                b.HasIndex(nameof(DevExpress.ExpressApp.Security.ISecurityUserLoginInfo.LoginProviderName), nameof(DevExpress.ExpressApp.Security.ISecurityUserLoginInfo.ProviderUserKey)).IsUnique();
            });
            modelBuilder.Entity<ModelDifference>()
                .HasMany(t => t.Aspects)
                .WithOne(t => t.Owner)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Order>().Property(p => p.Total).HasPrecision(18, 2);
            // CustomerSummaryRow.Revenue precision is declared via [Column(TypeName=...)] on the
            // property. We avoid Entity<CustomerSummaryRow>(...) here because touching the type
            // via modelBuilder would promote it from "query type" (auto-registered by SqlQuery<T>)
            // to a full entity, which then needs a key — and XAF would reject it.
        }
    }
}
