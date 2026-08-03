namespace TheShop.Web.Common;

/// <summary>
/// Well-known key constants for <see cref="BusyState"/>. Centralising them prevents
/// typos and makes it easy to find all callers for a given operation.
/// </summary>
public static class BusyKeys
{
    public const string Global = "global";

    /// <summary>Key for loading the admin console's module cards.</summary>
    public const string AdminDashboard = "admin.dashboard";

    /// <summary>Keys for auth-flow operations.</summary>
    public static class Auth
    {
        public const string SignIn = "auth.sign-in";
        public const string SignUp = "auth.sign-up";
        public const string SignInVerify = "auth.sign-in.verify";
        public const string SignUpVerify = "auth.sign-up.verify";
        public const string ResendOtp = "auth.resend-otp";
    }

    /// <summary>Keys for product-catalogue operations.</summary>
    public static class Products
    {
        public const string Catalogue = "products.catalogue";
        public const string Filters = "products.filters";
    }

    /// <summary>Keys for brand admin operations.</summary>
    public static class Brands
    {
        public const string AddBrand = "brands.add";
        public const string ManageList = "brands.manage-list";
        public const string EditBrand = "brands.edit";
        public const string BrandStatus = "brands.status";
        public const string DeleteBrands = "brands.delete";
    }

    /// <summary>Keys for category admin operations.</summary>
    public static class Categories
    {
        public const string AddCategory = "categories.add";
        public const string ManageList = "categories.manage-list";
        public const string EditCategory = "categories.edit";
        public const string CategoryStatus = "categories.status";
        public const string DeleteCategories = "categories.delete";
    }
}
