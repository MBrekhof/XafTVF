using Bogus;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.EF;
using DevExpress.ExpressApp.EFCore;
using DevExpress.ExpressApp.Security;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Updating;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl.EF;
using DevExpress.Persistent.BaseImpl.EF.PermissionPolicy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using XafTVF.Module.BusinessObjects;

namespace XafTVF.Module.DatabaseUpdate
{
    // For more typical usage scenarios, be sure to check out https://docs.devexpress.com/eXpressAppFramework/DevExpress.ExpressApp.Updating.ModuleUpdater
    public class Updater : ModuleUpdater
    {
        public Updater(IObjectSpace objectSpace, Version currentDBVersion) :
            base(objectSpace, currentDBVersion)
        {
        }
        public override void UpdateDatabaseAfterUpdateSchema()
        {
            base.UpdateDatabaseAfterUpdateSchema();
            //string name = "MyName";
            //EntityObject1 theObject = ObjectSpace.FirstOrDefault<EntityObject1>(u => u.Name == name);
            //if(theObject == null) {
            //    theObject = ObjectSpace.CreateObject<EntityObject1>();
            //    theObject.Name = name;
            //}

            // The code below creates users and roles for testing purposes only.
            // In production code, you can create users and assign roles to them automatically, as described in the following help topic:
            // https://docs.devexpress.com/eXpressAppFramework/119064/data-security-and-safety/security-system/authentication
#if !RELEASE
            // If a role doesn't exist in the database, create this role
            var defaultRole = CreateDefaultRole();
            var adminRole = CreateAdminRole();

            ObjectSpace.CommitChanges(); //This line persists created object(s).

            UserManager userManager = ObjectSpace.ServiceProvider.GetRequiredService<UserManager>();

            // If a user named 'User' doesn't exist in the database, create this user
            if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, "User") == null)
            {
                // Set a password if the standard authentication type is used
                string EmptyPassword = "";
                _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "User", EmptyPassword, (user) =>
                {
                    // Add the Users role to the user
                    user.Roles.Add(defaultRole);
                });
            }

            // If a user named 'Admin' doesn't exist in the database, create this user
            if (userManager.FindUserByName<ApplicationUser>(ObjectSpace, "Admin") == null)
            {
                // Set a password if the standard authentication type is used
                string EmptyPassword = "";
                _ = userManager.CreateUser<ApplicationUser>(ObjectSpace, "Admin", EmptyPassword, (user) =>
                {
                    // Add the Administrators role to the user
                    user.Roles.Add(adminRole);
                });
            }

            ObjectSpace.CommitChanges(); //This line persists created object(s).

            EnsureTvfExists();
            SeedCustomersAndOrders();
#endif
        }

        private void EnsureTvfExists()
        {
            if (ObjectSpace is not EFCoreObjectSpace efOs) return;
            var ctx = efOs.DbContext;

            // GCRecord = 0 filter respects XAF's deferred deletion — soft-deleted rows would
            // otherwise leak into the aggregate.
            const string sql = @"
CREATE OR ALTER FUNCTION dbo.get_top_customers
(
    @top_n int,
    @since datetime2
)
RETURNS TABLE
AS
RETURN
    SELECT TOP (@top_n)
           c.ID         AS CustomerId,
           c.Name       AS Name,
           SUM(o.Total) AS Revenue,
           COUNT(*)     AS OrderCount
    FROM   dbo.Customers c
    JOIN   dbo.Orders    o ON o.CustomerId = c.ID
    WHERE  o.OrderDate >= @since
      AND  c.GCRecord = 0
      AND  o.GCRecord = 0
    GROUP BY c.ID, c.Name
    ORDER BY SUM(o.Total) DESC;";

            ctx.Database.ExecuteSqlRaw(sql);
        }

        private void SeedCustomersAndOrders()
        {
            if (ObjectSpace is not EFCoreObjectSpace efOs) return;
            var ctx = (XafTVFEFCoreDbContext)efOs.DbContext;

            if (ctx.Customers.AsNoTracking().Any()) return;

            Randomizer.Seed = new Random(42);

            var customerFaker = new Faker<Customer>()
                .CustomInstantiator(_ => ctx.CreateProxy<Customer>())
                .RuleFor(c => c.Name, f => f.Company.CompanyName());

            var customers = customerFaker.Generate(1000);

            var orderFaker = new Faker<Order>()
                .CustomInstantiator(_ => ctx.CreateProxy<Order>())
                .RuleFor(o => o.OrderDate, f => f.Date.Past(3))
                .RuleFor(o => o.Total, f => Math.Round(f.Random.Decimal(10m, 5000m), 2));

            var rnd = new Random(42);
            var orders = new List<Order>(capacity: 60000);
            foreach (var c in customers)
            {
                int n = rnd.Next(20, 101); // 20..100 inclusive
                for (int i = 0; i < n; i++)
                {
                    var o = orderFaker.Generate();
                    o.Customer = c;
                    orders.Add(o);
                }
            }

            bool prevAutoDetect = ctx.ChangeTracker.AutoDetectChangesEnabled;
            try
            {
                ctx.ChangeTracker.AutoDetectChangesEnabled = false;
                ctx.Customers.AddRange(customers);
                ctx.Orders.AddRange(orders);
                ctx.SaveChanges();
            }
            finally
            {
                ctx.ChangeTracker.AutoDetectChangesEnabled = prevAutoDetect;
            }
        }
        public override void UpdateDatabaseBeforeUpdateSchema()
        {
            base.UpdateDatabaseBeforeUpdateSchema();
        }
        PermissionPolicyRole CreateAdminRole()
        {
            PermissionPolicyRole adminRole = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(r => r.Name == "Administrators");
            if (adminRole == null)
            {
                adminRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
                adminRole.Name = "Administrators";
                adminRole.IsAdministrative = true;
            }
            return adminRole;
        }
        PermissionPolicyRole CreateDefaultRole()
        {
            PermissionPolicyRole defaultRole = ObjectSpace.FirstOrDefault<PermissionPolicyRole>(role => role.Name == "Default");
            if (defaultRole == null)
            {
                defaultRole = ObjectSpace.CreateObject<PermissionPolicyRole>();
                defaultRole.Name = "Default";

                defaultRole.AddObjectPermissionFromLambda<ApplicationUser>(SecurityOperations.Read, cm => cm.ID == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddNavigationPermission(@"Application/NavigationItems/Items/Default/Items/MyDetails", SecurityPermissionState.Allow);
                defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "ChangePasswordOnFirstLogon", cm => cm.ID == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddMemberPermissionFromLambda<ApplicationUser>(SecurityOperations.Write, "StoredPassword", cm => cm.ID == (Guid)CurrentUserIdOperator.CurrentUserId(), SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<PermissionPolicyRole>(SecurityOperations.Read, SecurityPermissionState.Deny);
                defaultRole.AddObjectPermission<ModelDifference>(SecurityOperations.ReadWriteAccess, "UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
                defaultRole.AddObjectPermission<ModelDifferenceAspect>(SecurityOperations.ReadWriteAccess, "Owner.UserId = ToStr(CurrentUserId())", SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<ModelDifference>(SecurityOperations.Create, SecurityPermissionState.Allow);
                defaultRole.AddTypePermissionsRecursively<ModelDifferenceAspect>(SecurityOperations.Create, SecurityPermissionState.Allow);
            }
            return defaultRole;
        }
    }
}
