namespace TheShop.Domain.ValueObjects;

/// <summary>
/// The single source of truth for every permission code in the system, organized by admin
/// module. The <c>permissions</c> table seed is generated from <see cref="All"/> so the code and
/// the database can never silently drift (FR-3). Every module in this catalogue is an
/// admin-area module this release.
/// </summary>
public static class PermissionCatalogue
{

    public static class Products
    {
        public static readonly Permission View = Permission.Create("products.view");
        public static readonly Permission Create = Permission.Create("products.create");
        public static readonly Permission Edit = Permission.Create("products.edit");
        public static readonly Permission Delete = Permission.Create("products.delete");
    }

    public static class Categories
    {
        public static readonly Permission View = Permission.Create("categories.view");
        public static readonly Permission Create = Permission.Create("categories.create");
        public static readonly Permission Edit = Permission.Create("categories.edit");
        public static readonly Permission Delete = Permission.Create("categories.delete");
    }

    public static class Orders
    {
        public static readonly Permission View = Permission.Create("orders.view");
        public static readonly Permission Create = Permission.Create("orders.create");
        public static readonly Permission Edit = Permission.Create("orders.edit");
        public static readonly Permission Delete = Permission.Create("orders.delete");

        /// <summary>Sensitive action: issuing a refund. Reserved for Admin+ roles.</summary>
        public static readonly Permission Refund = Permission.Create("orders.refund");
    }

    public static class Customers
    {
        public static readonly Permission View = Permission.Create("customers.view");
        public static readonly Permission Create = Permission.Create("customers.create");
        public static readonly Permission Edit = Permission.Create("customers.edit");
        public static readonly Permission Delete = Permission.Create("customers.delete");

        /// <summary>Sensitive action: exporting customer data.</summary>
        public static readonly Permission Export = Permission.Create("customers.export");
    }

    public static class Coupons
    {
        public static readonly Permission View = Permission.Create("coupons.view");
        public static readonly Permission Create = Permission.Create("coupons.create");
        public static readonly Permission Edit = Permission.Create("coupons.edit");
        public static readonly Permission Delete = Permission.Create("coupons.delete");
    }

    public static class Promotions
    {
        public static readonly Permission View = Permission.Create("promotions.view");
        public static readonly Permission Create = Permission.Create("promotions.create");
        public static readonly Permission Edit = Permission.Create("promotions.edit");
        public static readonly Permission Delete = Permission.Create("promotions.delete");
    }

    public static class Reports
    {
        public static readonly Permission View = Permission.Create("reports.view");
        public static readonly Permission Create = Permission.Create("reports.create");
        public static readonly Permission Edit = Permission.Create("reports.edit");
        public static readonly Permission Delete = Permission.Create("reports.delete");
    }

    public static class Settings
    {
        public static readonly Permission View = Permission.Create("settings.view");
        public static readonly Permission Create = Permission.Create("settings.create");
        public static readonly Permission Edit = Permission.Create("settings.edit");
        public static readonly Permission Delete = Permission.Create("settings.delete");
    }

    public static class AdminUsers
    {
        public static readonly Permission View = Permission.Create("admin_users.view");
        public static readonly Permission Create = Permission.Create("admin_users.create");
        public static readonly Permission Edit = Permission.Create("admin_users.edit");
        public static readonly Permission Delete = Permission.Create("admin_users.delete");
    }

    public static class Roles
    {
        public static readonly Permission View = Permission.Create("roles.view");
        public static readonly Permission Create = Permission.Create("roles.create");
        public static readonly Permission Edit = Permission.Create("roles.edit");
        public static readonly Permission Delete = Permission.Create("roles.delete");
    }

    /// <summary>
    /// Every permission defined by the catalogue, across all ten modules. This is the
    /// generation source for the <c>permissions</c> table seed (FR-3).
    /// </summary>
    public static readonly IReadOnlyList<Permission> All =
    [
        Products.View, Products.Create, Products.Edit, Products.Delete,
        Categories.View, Categories.Create, Categories.Edit, Categories.Delete,
        Orders.View, Orders.Create, Orders.Edit, Orders.Delete, Orders.Refund,
        Customers.View, Customers.Create, Customers.Edit, Customers.Delete, Customers.Export,
        Coupons.View, Coupons.Create, Coupons.Edit, Coupons.Delete,
        Promotions.View, Promotions.Create, Promotions.Edit, Promotions.Delete,
        Reports.View, Reports.Create, Reports.Edit, Reports.Delete,
        Settings.View, Settings.Create, Settings.Edit, Settings.Delete,
        AdminUsers.View, AdminUsers.Create, AdminUsers.Edit, AdminUsers.Delete,
        Roles.View, Roles.Create, Roles.Edit, Roles.Delete,
    ];

    /// <summary>
    /// <c>true</c> when <paramref name="code"/> matches a known catalogue permission. Every
    /// module in this catalogue is admin-area this release, so membership in <see cref="All"/>
    /// is sufficient.
    /// </summary>
    public static bool IsAdminArea(string code) =>
        All.Any(p => string.Equals(p.Code, code, StringComparison.Ordinal));
}
