using TheShop.Web.Common.UI;

namespace TheShop.Web.Components.Admin;

/// <summary>
/// Loading placeholder for <see cref="AdminModuleCard"/>: label line, count line and action block
/// inside the same outlined frame and spacing. Decorative only.
/// </summary>
public partial class AdminModuleCardSkeleton : ShopComponentBase
{
    private string ClassName => ShopCssClass.Join("shop-native", "shop-admin-module-card-skeleton", Class);
}
