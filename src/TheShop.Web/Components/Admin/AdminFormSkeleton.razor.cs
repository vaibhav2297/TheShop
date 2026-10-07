using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Admin;

/// <summary>
/// Loading placeholder for the admin edit pages: heading line, one field and one form panel with
/// the loaded page's padding and section gap. Decorative only.
/// </summary>
public partial class AdminFormSkeleton : ShopComponentBase
{
    private string ClassName => ShopCssClass.Join("shop-native", "shop-admin-form-skeleton", Class);
}
